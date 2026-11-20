using System.Collections.Concurrent;
using Clio.Application;
using Clio.Domain;

namespace Clio.Infrastructure;

/// <summary>Thread-safe in-memory documents (slice 1; a database adapter replaces it in slice 2).</summary>
internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, TermSheetDocument> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byHash = new(StringComparer.Ordinal);

    public Task AddAsync(TermSheetDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_byHash.TryAdd(document.Sha256, document.Id)) throw new InvalidOperationException("A document with the same content already exists.");
        _byId[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task SaveAsync(TermSheetDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        _byId[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<TermSheetDocument?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<TermSheetDocument?> FindByHashAsync(string sha256, CancellationToken cancellationToken) =>
        Task.FromResult(_byHash.TryGetValue(sha256, out var id) ? _byId.GetValueOrDefault(id) : null);

    public Task<IReadOnlyList<TermSheetDocument>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TermSheetDocument>>(_byId.Values.OrderByDescending(d => d.UploadedAt).ThenBy(d => d.FileName, StringComparer.Ordinal).ToList());
}

/// <summary>Keeps originals in memory (slice 1; blob storage with encryption at rest in production).</summary>
internal sealed class InMemoryOriginalFileStore : IOriginalFileStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _files = new();

    public Task SaveAsync(Guid documentId, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        _files[documentId] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(_files.TryGetValue(documentId, out var c) ? c.ToArray() : null);
}
