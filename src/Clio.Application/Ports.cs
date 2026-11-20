using Agentic.Blocks.Core;
using Clio.Domain;

namespace Clio.Application;

/// <summary>A page of text, as lines (line numbers are index + 1).</summary>
public sealed record DocumentPage(int Number, IReadOnlyList<string> Lines);

/// <summary>The text of a document, page by page.</summary>
public sealed record DocumentText(IReadOnlyList<DocumentPage> Pages);

/// <summary>Turns an uploaded file into text. Fails with <c>document.unreadable</c> when there is no usable text.</summary>
public interface IDocumentTextReader
{
    /// <summary>Reads the file.</summary>
    Result<DocumentText> Read(byte[] content, string fileName);
}

/// <summary>
/// The swappable extraction model (rules today; an LLM or a local model later). Returns one candidate per field
/// with value, confidence and source; the domain validates everything it returns.
/// </summary>
public interface IExtractionModel
{
    /// <summary>Name and version, stored with every extraction (for example <c>rules-v1</c>).</summary>
    string Name { get; }

    /// <summary>Proposes values for the fields.</summary>
    Task<IReadOnlyList<FieldCandidate>> ExtractAsync(DocumentText text, CancellationToken cancellationToken);
}

/// <summary>Stores documents.</summary>
public interface IDocumentRepository
{
    /// <summary>Adds a new document.</summary>
    Task AddAsync(TermSheetDocument document, CancellationToken cancellationToken);

    /// <summary>Saves changes to a document.</summary>
    Task SaveAsync(TermSheetDocument document, CancellationToken cancellationToken);

    /// <summary>Finds by id.</summary>
    Task<TermSheetDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds by SHA-256 of the original.</summary>
    Task<TermSheetDocument?> FindByHashAsync(string sha256, CancellationToken cancellationToken);

    /// <summary>All documents, newest first.</summary>
    Task<IReadOnlyList<TermSheetDocument>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Keeps the original file, byte for byte, whatever happens to extraction.</summary>
public interface IOriginalFileStore
{
    /// <summary>Saves the original.</summary>
    Task SaveAsync(Guid documentId, byte[] content, CancellationToken cancellationToken);

    /// <summary>Loads the original.</summary>
    Task<byte[]?> GetAsync(Guid documentId, CancellationToken cancellationToken);
}

/// <summary>Hands documents to the extraction worker.</summary>
public interface IExtractionChannel
{
    /// <summary>Queues a document id (waits when the queue is full).</summary>
    ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Reads queued ids until cancelled.</summary>
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>An audit fact. Actors are user ids; reasons hold no personal data.</summary>
public sealed record AuditEvent(string Actor, string Action, string EntityType, string EntityId, string? Reason = null);

/// <summary>Append-only audit trail.</summary>
public interface IAuditTrail
{
    /// <summary>Appends an entry.</summary>
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>Integration event: all three terms of a document are captured.</summary>
public sealed record TermsCaptured(Guid DocumentId, string FileName, IReadOnlyDictionary<string, string> Terms, DateTimeOffset CapturedAt);

/// <summary>Publishes integration events reliably (outbox: committed with the unit of work, delivered at least once).</summary>
public interface IIntegrationEvents
{
    /// <summary>Stages the event.</summary>
    void Publish(TermsCaptured termsCaptured);
}

/// <summary>Where completed terms are handed to credit screening (a fake downstream in slice 1).</summary>
public interface IScreeningHandoffLog
{
    /// <summary>Most recent hand-offs.</summary>
    IReadOnlyList<TermsCaptured> Recent();
}

/// <summary>Extraction settings (config section <c>Extraction</c>).</summary>
public sealed class ExtractionSettings
{
    /// <summary>Fields at or above this confidence are saved without review. Tuned from the eval set.</summary>
    public decimal ReviewThreshold { get; set; } = 0.90m;

    /// <summary>Largest accepted upload.</summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Bounded queue size between the API and the worker.</summary>
    public int QueueCapacity { get; set; } = 500;

    /// <summary>Which <see cref="IExtractionModel"/> to use: <c>rules</c> (built) or <c>llm</c> (documented port).</summary>
    public string Model { get; set; } = "rules";
}
