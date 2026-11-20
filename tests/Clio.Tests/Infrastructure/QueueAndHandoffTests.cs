using Clio.Application;
using Clio.Infrastructure;
using Agentic.Blocks.Outbox;
using Xunit;

namespace Clio.Infrastructure.Tests;

public class QueueAndHandoffTests
{
    [Fact]
    public async Task Channel_queue_delivers_ids_in_order()
    {
        var queue = new ExtractionChannel(new ExtractionSettings { QueueCapacity = 4 });
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        await queue.EnqueueAsync(a, CancellationToken.None);
        await queue.EnqueueAsync(b, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var read = new List<Guid>();
        await foreach (var id in queue.ReadAllAsync(cts.Token))
        {
            read.Add(id);
            if (read.Count == 2) break;
        }

        Assert.Equal<IEnumerable<Guid>>(new[] { a, b }, read);
    }

    [Fact]
    public async Task Screening_handoff_is_idempotent_on_message_id()
    {
        var log = new InMemoryScreeningHandoffLog();
        var handler = new ScreeningHandoffHandler(log);
        var message = new TermsCaptured(Guid.NewGuid(), "ts.txt", new Dictionary<string, string> { ["Margin"] = "2.85" }, DateTimeOffset.UnixEpoch);
        var ctx = new OutboxMessageContext(Guid.NewGuid(), "terms.captured.v1", 1, null, DateTimeOffset.UnixEpoch);
        await handler.HandleAsync(message, ctx, CancellationToken.None);
        await handler.HandleAsync(message, ctx with { Attempt = 2 }, CancellationToken.None);
        Assert.Single(log.Recent());
    }
}
