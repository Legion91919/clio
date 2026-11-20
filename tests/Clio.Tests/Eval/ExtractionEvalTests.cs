using System.Runtime.CompilerServices;
using Clio.Application;
using Clio.Domain;
using Clio.Infrastructure;
using Xunit;

namespace Clio.Eval.Tests;

/// <summary>Runs the labelled eval set (eval/) through the rules extractor and fails below the agreed bar.</summary>
public class ExtractionEvalTests
{
    public const decimal MinFieldAccuracy = 0.85m;
    public const decimal MinAutoAcceptPrecision = 1.0m;

    private static string EvalDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "eval"));

    [Fact]
    public async Task Rules_extractor_meets_the_accuracy_bar_on_the_eval_set()
    {
        var cases = EvalSetLoader.Load(EvalDir());
        Assert.True(cases.Count >= 10, $"eval set has {cases.Count} documents");

        var report = await ExtractionEvaluator.RunAsync(new RulesExtractionModel(), cases, new ConfidenceThreshold(0.90m), new DateOnly(2026, 11, 20), CancellationToken.None);
        Console.WriteLine(report.ToText());

        Assert.Equal(3, report.Fields.Count);
        Assert.All(report.Fields, f => Assert.True(f.Accuracy >= MinFieldAccuracy, $"{f.Field} accuracy {f.Accuracy:P0} < {MinFieldAccuracy:P0}"));
        Assert.All(report.Fields, f => Assert.True(f.AutoAcceptPrecision >= MinAutoAcceptPrecision, $"{f.Field} auto-accept precision {f.AutoAcceptPrecision:P0}"));
    }

    [Fact]
    public async Task Evaluator_counts_a_wrong_auto_accepted_value_against_precision()
    {
        var text = new DocumentText([new DocumentPage(1, ["Margin: 9.99%"])]);
        var cases = new[] { new EvalCase("one", text, new Dictionary<TermField, string?> { [TermField.Margin] = "2.00" }) };
        var report = await ExtractionEvaluator.RunAsync(new RulesExtractionModel(), cases, new ConfidenceThreshold(0.90m), new DateOnly(2026, 11, 20), CancellationToken.None);
        var margin = report.Fields.Single(f => f.Field == TermField.Margin);
        Assert.Equal(0m, margin.Accuracy);
        Assert.Equal(0m, margin.AutoAcceptPrecision);
        Assert.Single(report.Misses);
    }
}
