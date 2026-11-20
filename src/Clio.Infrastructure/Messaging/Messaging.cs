using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Agentic.Blocks.Audit;
using Agentic.Blocks.Outbox;
using Clio.Application;

namespace Clio.Infrastructure;

/// <summary>Bounded in-process queue between upload and the extraction worker (System.Threading.Channels).</summary>
internal sealed class ExtractionChannel(ExtractionSettings settings) : IExtractionChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(settings.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(documentId, cancellationToken);

    public async IAsyncEnumerable<Guid> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return id;
    }
}

/// <summary>Writes to the hash-chained audit log of the Audit block. Audit failures are errors, never ignored.</summary>
internal sealed class BlocksAuditTrail(IAuditLog log) : IAuditTrail
{
    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var result = await log.AppendAsync(new AuditRecord(auditEvent.Actor, auditEvent.Action, auditEvent.EntityType, auditEvent.EntityId, auditEvent.Reason), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) throw new InvalidOperationException($"Audit write failed: {result.Error.Code}");
    }
}

/// <summary>Stages integration events in the outbox of the current unit of work.</summary>
internal sealed class OutboxIntegrationEvents(IOutbox outbox) : IIntegrationEvents
{
    public void Publish(TermsCaptured termsCaptured) => outbox.Enqueue(termsCaptured);
}

/// <summary>Fake downstream: the credit screening inbox. Idempotent on the outbox message id.</summary>
internal sealed class InMemoryScreeningHandoffLog : IScreeningHandoffLog
{
    private const int Keep = 200;
    private readonly ConcurrentDictionary<Guid, TermsCaptured> _byMessage = new();
    private readonly ConcurrentQueue<TermsCaptured> _order = new();

    public bool TryAdd(Guid messageId, TermsCaptured message)
    {
        if (!_byMessage.TryAdd(messageId, message)) return false;
        _order.Enqueue(message);
        while (_order.Count > Keep && _order.TryDequeue(out _))
        {
        }

        return true;
    }

    public IReadOnlyList<TermsCaptured> Recent() => _order.Reverse().ToList();
}

/// <summary>Delivers <see cref="TermsCaptured"/> to screening. At-least-once delivery, exactly-once effect.</summary>
internal sealed class ScreeningHandoffHandler(InMemoryScreeningHandoffLog log) : IOutboxHandler<TermsCaptured>
{
    public Task HandleAsync(TermsCaptured message, OutboxMessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        log.TryAdd(context.MessageId, message);
        return Task.CompletedTask;
    }
}
