using Agentic.Blocks.Core;

namespace Clio.Domain;

/// <summary>One extracted term with its evidence and review state.</summary>
public sealed class ExtractedField
{
    internal ExtractedField(TermField field, string? value, decimal confidence, SourceLocation? source, string? evidence, string? note, FieldStatus status)
    {
        Field = field;
        Value = value;
        Confidence = confidence;
        Source = source;
        Evidence = evidence;
        Note = note;
        Status = status;
    }

    /// <summary>Which term.</summary>
    public TermField Field { get; }

    /// <summary>Canonical value, or null when nothing valid was found.</summary>
    public string? Value { get; private set; }

    /// <summary>Model confidence after plausibility checks (0 to 1).</summary>
    public decimal Confidence { get; }

    /// <summary>Page and line of the evidence.</summary>
    public SourceLocation? Source { get; }

    /// <summary>The source text.</summary>
    public string? Evidence { get; }

    /// <summary>Why it needs review.</summary>
    public string? Note { get; }

    /// <summary>Review state.</summary>
    public FieldStatus Status { get; private set; }

    /// <summary>User id of the approver.</summary>
    public string? ApprovedBy { get; private set; }

    /// <summary>When it was approved (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>True when the value counts as captured.</summary>
    public bool IsSaved => Status is FieldStatus.AutoAccepted or FieldStatus.Approved;

    internal void Approve(string value, string userId, DateTimeOffset at)
    {
        Value = value;
        Status = FieldStatus.Approved;
        ApprovedBy = userId;
        ApprovedAt = at;
    }
}

/// <summary>An uploaded term sheet and the terms read from it. The aggregate root.</summary>
public sealed class TermSheetDocument
{
    // Replaced as a whole (never mutated in place) so readers on other threads always see a complete list.
    private List<ExtractedField> _fields = [];

    private TermSheetDocument(Guid id, string fileName, string contentType, long sizeBytes, string sha256, string uploadedBy, DateTimeOffset uploadedAt)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
    }

    /// <summary>Id.</summary>
    public Guid Id { get; }

    /// <summary>Original file name.</summary>
    public string FileName { get; }

    /// <summary>Content type as uploaded.</summary>
    public string ContentType { get; }

    /// <summary>Size of the original.</summary>
    public long SizeBytes { get; }

    /// <summary>SHA-256 of the original (hex); identical uploads map to one document.</summary>
    public string Sha256 { get; }

    /// <summary>User id of the uploader.</summary>
    public string UploadedBy { get; }

    /// <summary>Upload time (UTC).</summary>
    public DateTimeOffset UploadedAt { get; }

    /// <summary>Lifecycle state.</summary>
    public DocumentStatus Status { get; private set; } = DocumentStatus.Received;

    /// <summary>Machine-readable error code when unreadable or failed.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Human-readable error.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Model name and version that produced the fields.</summary>
    public string? Model { get; private set; }

    /// <summary>Fields, one per <see cref="TermField"/> once extracted.</summary>
    public IReadOnlyList<ExtractedField> Fields => _fields;

    /// <summary>Saved (auto-accepted or approved) values only.</summary>
    public IReadOnlyDictionary<TermField, string> SavedTerms =>
        _fields.Where(f => f.IsSaved && f.Value is not null).ToDictionary(f => f.Field, f => f.Value!);

    /// <summary>Creates a received document.</summary>
    public static TermSheetDocument Create(Guid id, string fileName, string contentType, long sizeBytes, string sha256, string uploadedBy, DateTimeOffset uploadedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        return new TermSheetDocument(id, fileName, contentType, sizeBytes, sha256, uploadedBy, uploadedAt);
    }

    /// <summary>Returns the field, if extracted.</summary>
    public ExtractedField? FieldOf(TermField field) => _fields.Find(f => f.Field == field);

    /// <summary>The worker picked it up.</summary>
    public Result StartProcessing()
    {
        if (Status is not (DocumentStatus.Received or DocumentStatus.Processing))
            return Error.Conflict("document.already_processed", "The document was already processed.");
        Status = DocumentStatus.Processing;
        return Result.Success();
    }

    /// <summary>No text could be read. The original stays in storage.</summary>
    public void MarkUnreadable(string code, string message) => Fail(DocumentStatus.Unreadable, code, message);

    /// <summary>Extraction failed unexpectedly. The original stays in storage.</summary>
    public void MarkFailed(string message) => Fail(DocumentStatus.Failed, "extraction.failed", message);

    /// <summary>
    /// Records model output: every value is validated (invalid becomes "no value, confidence 0"), plausibility rules
    /// run, then fields at or above <paramref name="threshold"/> are auto-accepted and the rest wait for review.
    /// </summary>
    public Result RecordExtraction(IEnumerable<FieldCandidate> candidates, ConfidenceThreshold threshold, string model, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(threshold);
        if (Status != DocumentStatus.Processing) return Error.Conflict("document.not_processing", "The document is not being processed.");
        var fields = new List<ExtractedField>();
        foreach (var raw in candidates.GroupBy(c => c.Field).Select(g => g.OrderByDescending(c => c.Confidence).First()))
        {
            var candidate = raw;
            if (candidate.Value is not null)
            {
                var normalized = FieldValueRules.Normalize(candidate.Field, candidate.Value);
                candidate = normalized.IsSuccess
                    ? candidate with { Value = normalized.Value }
                    : candidate with { Value = null, Confidence = 0m, Evidence = candidate.Evidence ?? candidate.Value, Note = $"Model value failed validation: {normalized.Error.Message}" };
            }

            candidate = PlausibilityRules.Check(candidate, today);
            var accepted = candidate.Value is not null && threshold.IsMetBy(candidate.Confidence);
            fields.Add(new ExtractedField(candidate.Field, candidate.Value, Math.Clamp(candidate.Confidence, 0m, 1m), candidate.Source, candidate.Evidence, candidate.Note,
                accepted ? FieldStatus.AutoAccepted : FieldStatus.NeedsReview));
        }

        foreach (var missing in Enum.GetValues<TermField>().Where(f => !fields.Exists(x => x.Field == f)))
            fields.Add(new ExtractedField(missing, null, 0m, null, null, "Not found in the document.", FieldStatus.NeedsReview));

        fields.Sort((a, b) => a.Field.CompareTo(b.Field));
        _fields = fields;
        Model = model;
        UpdateStatus();
        return Result.Success();
    }

    /// <summary>
    /// A person approves a field, optionally correcting the value. Approving again with the same value is a no-op
    /// (safe retry); a different value after approval is a conflict.
    /// </summary>
    public Result<ExtractedField> Approve(TermField field, string? correctedValue, string userId, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var target = FieldOf(field);
        if (target is null) return Error.NotFound("field.not_found", "The field has not been extracted yet.");
        var proposed = correctedValue ?? target.Value;
        if (proposed is null) return Error.Validation("field.value_required", "value", "Nothing was found for this field; enter the value to approve.");
        var normalized = FieldValueRules.Normalize(field, proposed);
        if (normalized.IsFailure) return normalized.Error;

        switch (target.Status)
        {
            case FieldStatus.Approved when target.Value == normalized.Value:
                return target;
            case FieldStatus.Approved:
                return Error.Conflict("field.already_approved", "The field was already approved with a different value.");
            case FieldStatus.AutoAccepted:
                return Error.Conflict("field.already_saved", "The field was auto-accepted and is already saved.");
        }

        target.Approve(normalized.Value, userId, at);
        UpdateStatus();
        return target;
    }

    private void UpdateStatus() =>
        Status = _fields.TrueForAll(f => f.IsSaved) ? DocumentStatus.Complete : DocumentStatus.NeedsReview;

    private void Fail(DocumentStatus status, string code, string message)
    {
        Status = status;
        ErrorCode = code;
        ErrorMessage = message;
        _fields = [];
    }
}
