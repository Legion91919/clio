using System.Diagnostics;
using Agentic.Blocks.Outbox;
using Clio.Application;

namespace Clio.Api.Worker;

/// <summary>
/// The extraction worker: a BackgroundService in the API process that drains the bounded channel. Each document is
/// its own unit of work (scope + outbox commit), so one bad file never stops the queue.
/// </summary>
internal sealed partial class ExtractionWorker(IExtractionChannel channel, IServiceScopeFactory scopes, ILogger<ExtractionWorker> logger) : BackgroundService
{
    internal static readonly ActivitySource Source = new("Clio.Extraction");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var documentId in channel.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            using var activity = Source.StartActivity("extract-document");
            activity?.SetTag("clio.document_id", documentId);
            var started = Stopwatch.GetTimestamp();
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
                try
                {
                    var result = await scope.ServiceProvider.GetRequiredService<TermSheetService>().ProcessAsync(documentId, stoppingToken).ConfigureAwait(false);
                    if (result.IsSuccess) await outbox.CommitAsync(stoppingToken).ConfigureAwait(false);
                    else outbox.Discard();
                    LogProcessed(logger, documentId, result.IsSuccess, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    outbox.Discard();
                    LogCrashed(logger, ex, documentId);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Document {DocumentId} processed (success: {Success}) in {ElapsedMs} ms")]
    private static partial void LogProcessed(ILogger logger, Guid documentId, bool success, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction worker failed on document {DocumentId}; continuing")]
    private static partial void LogCrashed(ILogger logger, Exception exception, Guid documentId);
}

/// <summary>Development only: uploads the invented samples in <c>Seed:SamplesPath</c> so the UI has data.</summary>
internal sealed partial class DemoDataSeeder(IHostEnvironment env, IConfiguration config, IServiceScopeFactory scopes, ILogger<DemoDataSeeder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = config["Seed:SamplesPath"];
        if (!env.IsDevelopment() || string.IsNullOrWhiteSpace(path)) return;
        var dir = Path.GetFullPath(Path.Combine(env.ContentRootPath, path));
        if (!Directory.Exists(dir)) return;
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var service = scope.ServiceProvider.GetRequiredService<TermSheetService>();
            foreach (var file in Directory.GetFiles(dir).Where(f => !Path.GetFileName(f).StartsWith('.')).Order(StringComparer.Ordinal))
            {
                var bytes = await File.ReadAllBytesAsync(file, stoppingToken).ConfigureAwait(false);
                var type = file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "text/plain";
                await service.UploadAsync(new UploadCommand(Path.GetFileName(file), type, bytes, "seed"), stoppingToken).ConfigureAwait(false);
            }

            LogSeeded(logger, dir);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded invented sample term sheets from {Directory}")]
    private static partial void LogSeeded(ILogger logger, string directory);
}
