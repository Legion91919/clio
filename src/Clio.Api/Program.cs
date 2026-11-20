using Clio.Api;
using Clio.Application;
using Clio.Domain;
using Clio.Infrastructure;

// `dotnet run --project src/Clio.Api -- eval [dir]` runs the labelled eval set and exits 1 below the bar.
if (args.Length > 0 && args[0] == "eval")
{
    var dir = args.Length > 1 ? args[1] : "eval";
    // `dotnet run` may start in the project folder: look upwards for the eval set.
    for (var probe = new DirectoryInfo(Directory.GetCurrentDirectory()); !Directory.Exists(dir) && probe?.Parent is not null; probe = probe.Parent)
    {
        var candidate = Path.Combine(probe.Parent.FullName, args.Length > 1 ? args[1] : "eval");
        if (Directory.Exists(candidate)) dir = candidate;
    }

    var report = await ExtractionEvaluator.RunAsync(DependencyInjection.CreateExtractionModel("rules"), EvalSetLoader.Load(dir), new ConfidenceThreshold(0.90m), DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
    Console.WriteLine(report.ToText());
    return report.Fields.All(f => f.Accuracy >= 0.85m && f.AutoAcceptPrecision >= 1.0m) ? 0 : 1;
}

var builder = WebApplication.CreateBuilder(args);
ClioApp.ConfigureServices(builder);
var app = builder.Build();
ClioApp.Configure(app);
await app.RunAsync();
return 0;
