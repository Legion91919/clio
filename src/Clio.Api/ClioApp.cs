using System.Text.Json.Serialization;
using Agentic.Blocks.Audit;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Outbox;
using Agentic.Blocks.Web;
using Clio.Api.Worker;
using Clio.Application;
using Clio.Infrastructure;

namespace Clio.Api;

/// <summary>Composition root, shared by Program and the acceptance tests.</summary>
public static class ClioApp
{
    /// <summary>Role that can upload and approve.</summary>
    public const string AnalystRole = "analyst";

    /// <summary>Read-only role (credit committee); also reads the audit log.</summary>
    public const string CommitteeRole = "committee";

    /// <summary>Policy: analysts only.</summary>
    public const string AnalystPolicy = "analyst";

    /// <summary>Policy: any signed-in Clio user.</summary>
    public const string ReaderPolicy = "reader";

    /// <summary>Registers every service.</summary>
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var settings = builder.Configuration.GetSection("Extraction").Get<ExtractionSettings>() ?? new ExtractionSettings();
        var demoSecret = builder.Configuration["Demo:Password"];

        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services
            .AddBlocksWeb()
            .AddBlocksAuth(o =>
            {
                o.DemoUsers.Add(Demo("analyst@example.com", demoSecret, AnalystRole));
                o.DemoUsers.Add(Demo("committee@example.com", demoSecret, CommitteeRole));
            })
            .AddClioInfrastructure(settings);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AnalystPolicy, p => p.RequireRole(AnalystRole))
            .AddPolicy(ReaderPolicy, p => p.RequireRole(AnalystRole, CommitteeRole));
        builder.Services.AddHostedService<ExtractionWorker>();
        builder.Services.AddHostedService<DemoDataSeeder>();
    }

    /// <summary>Builds the HTTP pipeline.</summary>
    public static void Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseBlocksWeb();
        app.UseBlocksOutbox();
        app.MapBlocksAuth("/auth");
        app.MapBlocksAudit("/audit", requiredRole: CommitteeRole);
        app.MapDocumentEndpoints();
        app.MapGet("/", () => Results.Ok(new { name = "Clio", purpose = "Extracts loan terms from term sheets", docs = "/api/documents" }));
    }

    private static DemoUser Demo(string email, string? secret, string role)
    {
        var user = new DemoUser { Email = email };
        if (!string.IsNullOrEmpty(secret)) user.Password = secret; // from config, never hard-coded. privacy-guard: ignore
        user.Roles.Add(role);
        return user;
    }
}
