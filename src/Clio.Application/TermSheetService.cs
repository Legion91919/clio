using System.Collections.Concurrent;
using System.Security.Cryptography;
using Agentic.Blocks.Core;
using Clio.Domain;

namespace Clio.Application;

/// <summary>Upload request.</summary>
public sealed record UploadCommand(string FileName, string ContentType, byte[] Content, string UserId);

/// <summary>Upload result; <see cref="Created"/> is false when the same file was uploaded before.</summary>
public sealed record UploadOutcome(TermSheetDocument Document, bool Created);

/// <summary>Approve (and optionally correct) one field.</summary>
public sealed record ApproveCommand(Guid DocumentId, TermField Field, string? Value, string UserId);

/// <summary>One field waiting for a person.</summary>
public sealed record ReviewItem(Guid DocumentId, string FileName, ExtractedField Field);

/// <summary>Serialises changes per document (in-process; a database adapter would use row versions).</summary>
public sealed class DocumentLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>Runs <paramref name="action"/> while holding the document's lock.</summary>
    public async Task<T> RunAsync<T>(Guid id, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var gate = _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>The slice-1 use cases: upload, process (worker), read, review queue, approve.</summary>
public sealed class TermSheetService(
    IDocumentRepository documents,
    IOriginalFileStore originals,
    IDocumentTextReader reader,
    IExtractionModel model,
    IExtractionChannel queue,
    IAuditTrail audit,
    IIntegrationEvents events,
    ExtractionSettings settings,
    DocumentLocks locks,
    TimeProvider time)
{
    /// <summary>System actor for worker actions.</summary>
    public const string SystemActor = "system:extractor";

    /// <summary>Audit entity type for documents.</summary>
    public const string DocumentEntity = "document";

    /// <summary>Audit entity type for fields; id is <c>{documentId}:{field}</c>.</summary>
    public const string FieldEntity = "term-field";

    /// <summary>Stores the original, creates the document and queues it. Same bytes again returns the existing document.</summary>
    public async Task<Result<UploadOutcome>> UploadAsync(UploadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Content.Length == 0) return Error.Validation("upload.empty", "file", "The file is empty.");
        if (command.Content.Length > settings.MaxUploadBytes)
            return Error.Validation("upload.too_large", "file", $"The file is larger than {settings.MaxUploadBytes / (1024 * 1024)} MB.");
        if (string.IsNullOrWhiteSpace(command.FileName)) return Error.Validation("upload.name", "file", "The file needs a name.");

        var sha = Convert.ToHexString(SHA256.HashData(command.Content));
        var existing = await documents.FindByHashAsync(sha, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return new UploadOutcome(existing, false);

        var document = TermSheetDocument.Create(Guid.NewGuid(), Path.GetFileName(command.FileName), command.ContentType, command.Content.Length, sha, command.UserId, time.GetUtcNow());
        await originals.SaveAsync(document.Id, command.Content, cancellationToken).ConfigureAwait(false);
        await documents.AddAsync(document, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(new AuditEvent(command.UserId, "document.uploaded", DocumentEntity, document.Id.ToString()), cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(document.Id, cancellationToken).ConfigureAwait(false);
        return new UploadOutcome(document, true);
    }

    /// <summary>Worker step: read text, extract, validate, route by confidence. Never deletes the original.</summary>
    public Task<Result> ProcessAsync(Guid documentId, CancellationToken cancellationToken) =>
        locks.RunAsync(documentId, async () =>
        {
            var document = await documents.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (document is null) return Result.Failure(Error.NotFound("document.not_found", "Unknown document."));
            var started = document.StartProcessing();
            if (started.IsFailure) return started;

            try
            {
                var content = await originals.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
                var text = content is null
                    ? Result.Failure<DocumentText>(Error.Failure("document.original_missing", "The original file is missing."))
                    : reader.Read(content, document.FileName);
                if (text.IsFailure)
                {
                    document.MarkUnreadable(text.Error.Code, text.Error.Message);
                    await documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
                    await audit.RecordAsync(new AuditEvent(SystemActor, "document.unreadable", DocumentEntity, documentId.ToString(), text.Error.Code), cancellationToken).ConfigureAwait(false);
                    return Result.Success();
                }

                var candidates = await model.ExtractAsync(text.Value, cancellationToken).ConfigureAwait(false);
                var recorded = document.RecordExtraction(candidates, new ConfidenceThreshold(settings.ReviewThreshold), model.Name, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
                if (recorded.IsFailure) return recorded;
                await documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
                foreach (var field in document.Fields.Where(f => f.Status == FieldStatus.AutoAccepted))
                {
                    await audit.RecordAsync(new AuditEvent(SystemActor, "field.auto_accepted", FieldEntity, $"{documentId}:{field.Field}",
                        $"model={model.Name}; confidence={field.Confidence}; page={field.Source?.Page}; line={field.Source?.Line}"), cancellationToken).ConfigureAwait(false);
                }

                PublishIfComplete(document);
                return Result.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                document.MarkFailed("Extraction failed. The original is kept; try again or key the terms by hand.");
                await documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
                await audit.RecordAsync(new AuditEvent(SystemActor, "document.failed", DocumentEntity, documentId.ToString(), ex.GetType().Name), cancellationToken).ConfigureAwait(false);
                return Result.Failure(Error.Failure("extraction.failed", "Extraction failed."));
            }
        }, cancellationToken);

    /// <summary>Approves one field and records who did it.</summary>
    public Task<Result<ExtractedField>> ApproveAsync(ApproveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return locks.RunAsync(command.DocumentId, async () =>
        {
            var document = await documents.GetAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
            if (document is null) return Result.Failure<ExtractedField>(Error.NotFound("document.not_found", "Unknown document."));
            var wasApproved = document.FieldOf(command.Field)?.Status == FieldStatus.Approved;
            var wasComplete = document.Status == DocumentStatus.Complete;
            var approved = document.Approve(command.Field, command.Value, command.UserId, time.GetUtcNow());
            if (approved.IsFailure || wasApproved) return approved;

            await documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
            var f = approved.Value;
            await audit.RecordAsync(new AuditEvent(command.UserId, "field.approved", FieldEntity, $"{document.Id}:{f.Field}",
                $"page={f.Source?.Page}; line={f.Source?.Line}; corrected={(command.Value is not null).ToString().ToUpperInvariant()}"), cancellationToken).ConfigureAwait(false);
            if (!wasComplete) PublishIfComplete(document);
            return approved;
        }, cancellationToken);
    }

    /// <summary>Gets a document.</summary>
    public async Task<Result<TermSheetDocument>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await documents.GetAsync(id, cancellationToken).ConfigureAwait(false) is { } d
            ? d
            : Error.NotFound("document.not_found", "Unknown document.");

    /// <summary>Lists documents, newest first.</summary>
    public Task<IReadOnlyList<TermSheetDocument>> ListAsync(CancellationToken cancellationToken) => documents.ListAsync(cancellationToken);

    /// <summary>Fields under the threshold across all documents, oldest document first.</summary>
    public async Task<IReadOnlyList<ReviewItem>> GetReviewQueueAsync(CancellationToken cancellationToken) =>
        (await documents.ListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(d => d.UploadedAt)
            .SelectMany(d => d.Fields.Where(f => f.Status == FieldStatus.NeedsReview).Select(f => new ReviewItem(d.Id, d.FileName, f)))
            .ToList();

    /// <summary>The original bytes (kept for every document, readable or not).</summary>
    public async Task<Result<(TermSheetDocument Document, byte[] Content)>> GetOriginalAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken).ConfigureAwait(false);
        var content = document is null ? null : await originals.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return document is null || content is null
            ? Error.NotFound("document.not_found", "Unknown document.")
            : (document, content);
    }

    /// <summary>One page of text, so every value can be checked against its source.</summary>
    public async Task<Result<DocumentPage>> GetPageAsync(Guid id, int page, CancellationToken cancellationToken)
    {
        var original = await GetOriginalAsync(id, cancellationToken).ConfigureAwait(false);
        if (original.IsFailure) return original.Error;
        var text = reader.Read(original.Value.Content, original.Value.Document.FileName);
        if (text.IsFailure) return text.Error;
        return text.Value.Pages.FirstOrDefault(p => p.Number == page) is { } found
            ? found
            : Error.NotFound("page.not_found", $"The document has {text.Value.Pages.Count} page(s).");
    }

    private void PublishIfComplete(TermSheetDocument document)
    {
        if (document.Status != DocumentStatus.Complete) return;
        events.Publish(new TermsCaptured(document.Id, document.FileName,
            document.SavedTerms.ToDictionary(t => t.Key.ToString(), t => t.Value), time.GetUtcNow()));
    }
}
