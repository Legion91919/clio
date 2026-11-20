using System.Text.Json;
using Clio.Application;
using Clio.Domain;

namespace Clio.Infrastructure;

/// <summary>
/// Loads the labelled eval set: <c>labels.json</c> (file, loanAmount, margin, maturity; null = not stated)
/// and the documents under <c>documents/</c>.
/// </summary>
public static class EvalSetLoader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Loads every labelled case in <paramref name="directory"/>.</summary>
    public static IReadOnlyList<EvalCase> Load(string directory)
    {
        var labels = JsonSerializer.Deserialize<List<Label>>(File.ReadAllText(Path.Combine(directory, "labels.json")), Json)
            ?? throw new InvalidDataException("labels.json is empty.");
        var reader = new CompositeDocumentTextReader();
        return labels.Select(l =>
        {
            var text = reader.Read(File.ReadAllBytes(Path.Combine(directory, "documents", l.File)), l.File);
            if (text.IsFailure) throw new InvalidDataException($"Eval document {l.File} is unreadable: {text.Error.Message}");
            return new EvalCase(l.File, text.Value, new Dictionary<TermField, string?>
            {
                [TermField.LoanAmount] = l.LoanAmount,
                [TermField.Margin] = l.Margin,
                [TermField.Maturity] = l.Maturity,
            });
        }).ToList();
    }

    private sealed record Label(string File, string? LoanAmount, string? Margin, string? Maturity);
}
