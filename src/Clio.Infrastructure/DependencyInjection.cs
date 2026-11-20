using Agentic.Blocks.Audit;
using Agentic.Blocks.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Clio.Application;

namespace Clio.Infrastructure;

/// <summary>Wires the adapters behind the Application ports.</summary>
public static class DependencyInjection
{
    /// <summary>Outbox type name of <see cref="TermsCaptured"/>; stable across renames.</summary>
    public const string TermsCapturedMessage = "terms.captured.v1";

    /// <summary>Registers readers, the extraction model, stores, the channel, audit and outbox adapters and the use cases.</summary>
    public static IServiceCollection AddClioInfrastructure(this IServiceCollection services, ExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = new Domain.ConfidenceThreshold(settings.ReviewThreshold); // fail fast on a bad threshold
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
        services.AddSingleton<IOriginalFileStore, InMemoryOriginalFileStore>();
        services.AddSingleton<IDocumentTextReader, CompositeDocumentTextReader>();
        services.AddSingleton<IExtractionChannel, ExtractionChannel>();
        services.AddSingleton<DocumentLocks>();
        services.AddSingleton<IExtractionModel>(CreateModel(settings.Model));

        services.AddBlocksAudit();
        services.AddSingleton<IAuditTrail, BlocksAuditTrail>();
        services.AddBlocksOutbox();
        services.AddOutboxHandler<TermsCaptured, ScreeningHandoffHandler>(TermsCapturedMessage);
        services.AddSingleton<InMemoryScreeningHandoffLog>();
        services.AddSingleton<IScreeningHandoffLog>(sp => sp.GetRequiredService<InMemoryScreeningHandoffLog>());
        services.AddScoped<IIntegrationEvents, OutboxIntegrationEvents>();
        services.AddScoped<TermSheetService>();
        return services;
    }

    /// <summary>Creates the configured extraction model (used by the eval command).</summary>
    public static IExtractionModel CreateExtractionModel(string name) => CreateModel(name);

    private static RulesExtractionModel CreateModel(string name) => name.ToUpperInvariant() switch
    {
        "RULES" => new RulesExtractionModel(),
        "LLM" => throw new InvalidOperationException(
            "Extraction:Model=llm is a documented port, not built in slice 1. See .agentic/decisions/0003-pluggable-extraction-model.md and prompts/."),
        _ => throw new InvalidOperationException($"Unknown Extraction:Model '{name}'. Use 'rules'."),
    };
}
