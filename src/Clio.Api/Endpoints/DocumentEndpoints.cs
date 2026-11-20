using System.Security.Claims;
using Agentic.Blocks.Core;
using Agentic.Blocks.Web;
using Clio.Application;
using Clio.Domain;

namespace Clio.Api;

/// <summary>Source of a value.</summary>
public sealed record SourceDto(int Page, int Line);

/// <summary>One field as the UI sees it.</summary>
public sealed record FieldDto(string Field, string? Value, decimal Confidence, SourceDto? Source, string? Evidence, string? Note, string Status, string? ApprovedBy, DateTimeOffset? ApprovedAt);

/// <summary>Why a document could not be read.</summary>
public sealed record ErrorDto(string Code, string Message);

/// <summary>A document with its fields and saved terms.</summary>
public sealed record DocumentDto(Guid Id, string FileName, string Status, long SizeBytes, DateTimeOffset UploadedAt, string? Model, ErrorDto? Error,
    IReadOnlyList<FieldDto> Fields, IReadOnlyDictionary<string, string> Terms);

/// <summary>A field in the review queue.</summary>
public sealed record ReviewItemDto(Guid DocumentId, string FileName, string Field, string? Value, decimal Confidence, SourceDto? Source, string? Note);

/// <summary>Approve body; <see cref="Value"/> corrects the proposed value.</summary>
public sealed record ApproveRequest(string? Value);

/// <summary>HTTP endpoints for documents, the review queue and the screening hand-off log.</summary>
public static class DocumentEndpoints
{
    /// <summary>Maps <c>/api/...</c>.</summary>
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(ClioApp.ReaderPolicy).RequireRateLimiting(BlocksRateLimitPolicies.Api).WithTags("Documents");

        api.MapPost("/documents", async (IFormFile? file, ClaimsPrincipal user, TermSheetService service, CancellationToken ct) =>
        {
            if (file is null) return Error.Validation("upload.missing", "file", "Send the term sheet as multipart form field 'file'.").ToProblem();
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct).ConfigureAwait(false);
            var result = await service.UploadAsync(new UploadCommand(file.FileName, file.ContentType, buffer.ToArray(), user.GetUserId()!), ct).ConfigureAwait(false);
            return result.ToHttpResult(o => o.Created
                ? Results.Accepted($"/api/documents/{o.Document.Id}", ToDto(o.Document))
                : Results.Ok(ToDto(o.Document)));
        }).RequireAuthorization(ClioApp.AnalystPolicy).DisableAntiforgery();

        api.MapGet("/documents", async (TermSheetService service, CancellationToken ct) =>
            Results.Ok((await service.ListAsync(ct).ConfigureAwait(false)).Select(ToDto)));

        api.MapGet("/documents/{id:guid}", async (Guid id, TermSheetService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct).ConfigureAwait(false)).ToHttpResult(d => Results.Ok(ToDto(d))));

        api.MapGet("/documents/{id:guid}/original", async (Guid id, TermSheetService service, CancellationToken ct) =>
            (await service.GetOriginalAsync(id, ct).ConfigureAwait(false)).ToHttpResult(o =>
                Results.File(o.Content, string.IsNullOrWhiteSpace(o.Document.ContentType) ? "application/octet-stream" : o.Document.ContentType, o.Document.FileName)));

        api.MapGet("/documents/{id:guid}/pages/{page:int}", async (Guid id, int page, TermSheetService service, CancellationToken ct) =>
            (await service.GetPageAsync(id, page, ct).ConfigureAwait(false)).ToHttpResult(p => Results.Ok(new { number = p.Number, lines = p.Lines })));

        api.MapPost("/documents/{id:guid}/fields/{field}/approve", async (Guid id, string field, ApproveRequest? body, ClaimsPrincipal user, TermSheetService service, CancellationToken ct) =>
        {
            if (!Enum.TryParse<TermField>(field, ignoreCase: true, out var termField) || !Enum.IsDefined(termField))
                return Error.NotFound("field.unknown", $"Unknown field '{field}'. Use LoanAmount, Margin or Maturity.").ToProblem();
            var result = await service.ApproveAsync(new ApproveCommand(id, termField, string.IsNullOrWhiteSpace(body?.Value) ? null : body.Value, user.GetUserId()!), ct).ConfigureAwait(false);
            return result.ToHttpResult(f => Results.Ok(ToDto(f)));
        }).RequireAuthorization(ClioApp.AnalystPolicy);

        api.MapGet("/review-queue", async (TermSheetService service, CancellationToken ct) =>
            Results.Ok((await service.GetReviewQueueAsync(ct).ConfigureAwait(false)).Select(i =>
                new ReviewItemDto(i.DocumentId, i.FileName, i.Field.Field.ToString(), i.Field.Value, i.Field.Confidence, ToDto(i.Field.Source), i.Field.Note))));

        api.MapGet("/screening/handoffs", (IScreeningHandoffLog log) => Results.Ok(log.Recent()));
    }

    private static DocumentDto ToDto(TermSheetDocument d) => new(
        d.Id, d.FileName, d.Status.ToString(), d.SizeBytes, d.UploadedAt, d.Model,
        d.ErrorCode is null ? null : new ErrorDto(d.ErrorCode, d.ErrorMessage ?? string.Empty),
        d.Fields.Select(ToDto).ToList(),
        d.SavedTerms.ToDictionary(t => t.Key.ToString(), t => t.Value));

    private static FieldDto ToDto(ExtractedField f) =>
        new(f.Field.ToString(), f.Value, f.Confidence, ToDto(f.Source), f.Evidence, f.Note, f.Status.ToString(), f.ApprovedBy, f.ApprovedAt);

    private static SourceDto? ToDto(SourceLocation? s) => s is null ? null : new SourceDto(s.Page, s.Line);
}
