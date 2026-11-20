using System.Globalization;
using System.Text;
using Clio.Domain;

namespace Clio.Application;

/// <summary>A labelled document. A null expected value means "the document does not state it" (the model should abstain).</summary>
public sealed record EvalCase(string Name, DocumentText Text, IReadOnlyDictionary<TermField, string?> Expected);

/// <summary>Score for one field over the eval set.</summary>
public sealed record FieldScore(TermField Field, int Total, int Correct, int AutoAccepted, int AutoAcceptedCorrect)
{
    /// <summary>Share of documents where the final proposed value equals the label.</summary>
    public decimal Accuracy => Total == 0 ? 1m : Math.Round((decimal)Correct / Total, 4);

    /// <summary>Share of auto-accepted values that are right (the number that matters for skipping review).</summary>
    public decimal AutoAcceptPrecision => AutoAccepted == 0 ? 1m : Math.Round((decimal)AutoAcceptedCorrect / AutoAccepted, 4);

    /// <summary>Share of documents where the field skipped review.</summary>
    public decimal Coverage => Total == 0 ? 0m : Math.Round((decimal)AutoAccepted / Total, 4);
}

/// <summary>Eval result.</summary>
public sealed record EvalReport(string Model, decimal Threshold, IReadOnlyList<FieldScore> Fields, IReadOnlyList<string> Misses)
{
    /// <summary>A plain-text table for logs and CI output.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Eval: model={Model} threshold={Threshold}");
        sb.AppendLine("| Field | Docs | Accuracy | Auto-accepted | Auto-accept precision |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var f in Fields)
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {f.Field} | {f.Total} | {f.Accuracy:P0} | {f.Coverage:P0} | {f.AutoAcceptPrecision:P0} |");
        foreach (var miss in Misses) sb.AppendLine(CultureInfo.InvariantCulture, $"miss: {miss}");
        return sb.ToString();
    }
}

/// <summary>Runs a model over labelled documents through the same validation and plausibility rules as production.</summary>
public static class ExtractionEvaluator
{
    /// <summary>Evaluates <paramref name="model"/>.</summary>
    public static async Task<EvalReport> RunAsync(IExtractionModel model, IEnumerable<EvalCase> cases, ConfidenceThreshold threshold, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(threshold);
        var tallies = Enum.GetValues<TermField>().ToDictionary(f => f, _ => new int[4]);
        var misses = new List<string>();
        foreach (var c in cases)
        {
            var document = TermSheetDocument.Create(Guid.NewGuid(), c.Name, "text/plain", 1, "eval", "eval", DateTimeOffset.UnixEpoch);
            document.StartProcessing();
            document.RecordExtraction(await model.ExtractAsync(c.Text, cancellationToken).ConfigureAwait(false), threshold, model.Name, today);
            foreach (var (field, expected) in c.Expected)
            {
                var actual = document.FieldOf(field)!;
                var expectedNorm = expected is null ? null : FieldValueRules.Normalize(field, expected) is { IsSuccess: true } n ? n.Value : expected;
                var correct = actual.Value == expectedNorm;
                var auto = actual.Status == FieldStatus.AutoAccepted;
                var t = tallies[field];
                t[0]++;
                if (correct) t[1]++;
                if (auto) t[2]++;
                if (auto && correct) t[3]++;
                if (!correct) misses.Add(string.Create(CultureInfo.InvariantCulture, $"{c.Name} {field}: expected '{expectedNorm}' got '{actual.Value}' (confidence {actual.Confidence}, {actual.Status})"));
            }
        }

        var scores = tallies.Select(kv => new FieldScore(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3])).ToList();
        return new EvalReport(model.Name, threshold.Value, scores, misses);
    }
}
