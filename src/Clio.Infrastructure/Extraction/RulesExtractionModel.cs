using System.Globalization;
using System.Text.RegularExpressions;
using Clio.Application;
using Clio.Domain;

namespace Clio.Infrastructure;

/// <summary>
/// Deterministic extractor: known labels ("Facility Amount:", "Margin:", "Final Maturity Date:") give high confidence,
/// weaker labels, symbols, dd/mm dates, prose, step-ups and conflicts give less, so a person sees them.
/// Confidence values are calibrated against the eval set in <c>eval/</c>.
/// </summary>
internal sealed partial class RulesExtractionModel : IExtractionModel
{
    private static readonly (string Label, decimal Confidence)[] AmountLabels =
        [("facility amount", 0.97m), ("loan amount", 0.97m), ("principal amount", 0.97m), ("commitment", 0.95m), ("total commitments", 0.92m), ("facility size", 0.92m), ("amount", 0.90m)];

    private static readonly (string Label, decimal Confidence)[] MarginLabels =
        [("margin", 0.96m), ("applicable margin", 0.96m), ("interest margin", 0.96m), ("pricing", 0.93m)];

    private static readonly (string Label, decimal Confidence)[] MaturityLabels =
        [("final maturity date", 0.96m), ("maturity date", 0.96m), ("termination date", 0.95m), ("final repayment date", 0.95m), ("maturity", 0.92m), ("tenor", 0.92m)];

    private const decimal TableLayoutPenalty = 0.02m;
    private const decimal ProseConfidence = 0.75m;
    private const decimal AmbiguousCap = 0.60m;

    public string Name => "rules-v1";

    public Task<IReadOnlyList<FieldCandidate>> ExtractAsync(DocumentText text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new Dictionary<TermField, List<(FieldCandidate Candidate, bool Labelled)>>();
        foreach (var page in text.Pages)
        {
            for (var i = 0; i < page.Lines.Count; i++)
            {
                var line = page.Lines[i];
                var at = new SourceLocation(page.Number, i + 1);
                var labelled = LabelledLine().Match(line);
                if (labelled.Success)
                {
                    var label = Squash(labelled.Groups["label"].Value);
                    var value = labelled.Groups["value"].Value.Trim();
                    var penalty = labelled.Groups["sep"].Value.Trim().Length == 0 ? TableLayoutPenalty : 0m;
                    var hit = false;
                    hit |= TryLabel(found, AmountLabels, label, penalty, v => ParseAmount(v), TermField.LoanAmount, value, line, at);
                    hit |= TryLabel(found, MarginLabels, label, penalty, v => ParseMargin(v), TermField.Margin, value, line, at);
                    hit |= TryLabel(found, MaturityLabels, label, penalty, v => ParseMaturity(v), TermField.Maturity, value, line, at);
                    if (hit) continue;
                }

                Prose(found, line, at);
            }
        }

        var result = Enum.GetValues<TermField>().Select(f => Decide(f, found.GetValueOrDefault(f))).ToList();
        return Task.FromResult<IReadOnlyList<FieldCandidate>>(result);
    }

    private static bool TryLabel(Dictionary<TermField, List<(FieldCandidate, bool)>> found, (string Label, decimal Confidence)[] labels, string label, decimal penalty,
        Func<string, Parsed?> parse, TermField field, string value, string line, SourceLocation at)
    {
        var match = Array.Find(labels, l => l.Label == label);
        if (match.Label is null) return false;
        var parsed = parse(value);
        var candidate = parsed?.Value is null
            ? new FieldCandidate(field, null, 0.30m, at, line, parsed?.Note ?? $"Label found but no {field} value could be read.")
            : new FieldCandidate(field, parsed.Value, Math.Max(0m, match.Confidence - penalty - parsed.Penalty), at, line, parsed.Note);
        Add(found, field, candidate, labelled: true);
        return true;
    }

    private static void Prose(Dictionary<TermField, List<(FieldCandidate, bool)>> found, string line, SourceLocation at)
    {
        var lower = line.ToLowerInvariant();
        const string note = "Found in running text, not a labelled term.";
        if ((lower.Contains("facility", StringComparison.Ordinal) || lower.Contains("loan", StringComparison.Ordinal)) && ParseAmount(line) is { } amount)
            Add(found, TermField.LoanAmount, new FieldCandidate(TermField.LoanAmount, amount.Value, ProseConfidence - amount.Penalty, at, line, note), labelled: false);
        if ((lower.Contains("margin", StringComparison.Ordinal) || BaseRateWords().IsMatch(line)) && ParseMargin(line) is { Value: not null } margin)
            Add(found, TermField.Margin, new FieldCandidate(TermField.Margin, margin.Value, Math.Min(ProseConfidence, ProseConfidence - margin.Penalty), at, line, margin.Note ?? note), labelled: false);
        if (MaturityWords().IsMatch(line) && ParseMaturity(line) is { Value: not null } maturity)
            Add(found, TermField.Maturity, new FieldCandidate(TermField.Maturity, maturity.Value, ProseConfidence - maturity.Penalty, at, line, maturity.Note ?? note), labelled: false);
    }

    private static void Add(Dictionary<TermField, List<(FieldCandidate, bool)>> found, TermField field, FieldCandidate candidate, bool labelled)
    {
        if (!found.TryGetValue(field, out var list)) found[field] = list = [];
        list.Add((candidate, labelled));
    }

    private static FieldCandidate Decide(TermField field, List<(FieldCandidate Candidate, bool Labelled)>? all)
    {
        if (all is null || all.Count == 0) return new FieldCandidate(field, null, 0m, null, null, "Not found in the document.");
        var pool = all.Exists(x => x.Labelled && x.Candidate.Value is not null) ? all.Where(x => x.Labelled).ToList() : all;
        var best = pool.OrderByDescending(x => x.Candidate.Confidence).First().Candidate;
        var distinct = pool.Where(x => x.Candidate.Value is not null).Select(x => x.Candidate).DistinctBy(c => c.Value).ToList();
        if (distinct.Count > 1)
        {
            var where = string.Join(" and ", distinct.Select(c => $"p{c.Source?.Page} line {c.Source?.Line}"));
            return best with { Confidence = Math.Min(best.Confidence, AmbiguousCap), Note = $"Conflicting values on {where}." };
        }

        return best;
    }

    private sealed record Parsed(string? Value, decimal Penalty = 0m, string? Note = null);

    private static Parsed? ParseAmount(string text)
    {
        var m = AmountRegex().Match(text);
        if (!m.Success) return null;
        var (currency, penalty) = m.Groups["cur"].Value switch
        {
            "R" => ("ZAR", 0.04m),
            "$" => ("USD", 0.04m),
            "US$" => ("USD", 0.02m),
            "€" => ("EUR", 0.04m),
            "£" => ("GBP", 0.04m),
            var code => (code.ToUpperInvariant(), 0m),
        };
        var digits = m.Groups["num"].Value.Replace(",", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)) return null;
        amount *= m.Groups["mult"].Value.ToLowerInvariant() switch { "million" or "mn" or "m" => 1_000_000m, "billion" or "bn" => 1_000_000_000m, _ => 1m };
        return new Parsed(string.Create(CultureInfo.InvariantCulture, $"{currency} {amount:0.00}"), penalty, penalty > 0 ? $"Currency read from the symbol '{m.Groups["cur"].Value}'." : null);
    }

    private static Parsed? ParseMargin(string text)
    {
        var values = new List<decimal>();
        foreach (Match m in PercentRegex().Matches(text)) values.Add(decimal.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture));
        foreach (Match m in BpsRegex().Matches(text)) values.Add(decimal.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture) / 100m);
        if (values.Count == 0) return null;
        var first = PercentRegex().Match(text) is { Success: true } p && (BpsRegex().Match(text) is not { Success: true } b || p.Index < b.Index)
            ? decimal.Parse(p.Groups["n"].Value, CultureInfo.InvariantCulture)
            : values[^1];
        var value = first.ToString("0.00##", CultureInfo.InvariantCulture);
        return values.Distinct().Count() > 1
            ? new Parsed(value, 0.96m - AmbiguousCap, "More than one rate on the line (step-up or ratchet?); the first was taken.")
            : new Parsed(value);
    }

    private static Parsed? ParseMaturity(string text)
    {
        if (IsoDate().Match(text) is { Success: true } iso && DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
            return new Parsed(Iso(d1));
        if (DayMonthYear().Match(text) is { Success: true } dmy && TryDate(dmy.Groups["d"].Value, dmy.Groups["m"].Value, dmy.Groups["y"].Value, out var d2))
            return new Parsed(Iso(d2));
        if (MonthDayYear().Match(text) is { Success: true } mdy && TryDate(mdy.Groups["d"].Value, mdy.Groups["m"].Value, mdy.Groups["y"].Value, out var d3))
            return new Parsed(Iso(d3), 0.02m);
        if (SlashDate().Match(text) is { Success: true } sl && DateOnly.TryParseExact(sl.Value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d4))
            return new Parsed(Iso(d4), 0.10m, "Read as day/month/year; check the order.");
        if (RelativeTerm().IsMatch(text))
            return new Parsed(null, 0m, "Maturity is relative (for example years from first drawdown); a person must work out the date.");
        return null;
    }

    private static bool TryDate(string day, string month, string year, out DateOnly date) =>
        DateOnly.TryParseExact($"{day} {month[..3]} {year}", "d MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Squash(string label) => WhiteSpace().Replace(label.Trim().ToLowerInvariant(), " ");

    [GeneratedRegex(@"^\s*(?<label>[A-Za-z][A-Za-z /]{1,40}?)(?<sep>\s*[:–—]\s*|\s+-\s+|\s{2,})(?<value>\S.*)$")]
    private static partial Regex LabelledLine();

    [GeneratedRegex(@"(?<![A-Za-z])(?<cur>ZAR|USD|EUR|GBP|US\$|R|\$|€|£)\s?(?<num>\d{1,3}(?:[, ]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)(?:\s*(?<mult>million|mn|m|billion|bn)\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"(?<n>\d+(?:\.\d+)?)\s*(?:%|per\s?cent|percent|pct)", RegexOptions.IgnoreCase)]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"(?<n>\d+(?:\.\d+)?)\s*(?:bps|bp|basis points)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BpsRegex();

    [GeneratedRegex(@"\b(?:plus|above|over)\b.*\b(?:JIBAR|prime|SOFR|SONIA|EURIBOR)\b|\b(?:JIBAR|prime|SOFR|SONIA|EURIBOR)\b.*(?:\bplus\b|\+)", RegexOptions.IgnoreCase)]
    private static partial Regex BaseRateWords();

    [GeneratedRegex(@"\b(?:repayable|repaid|matur\w*|final repayment)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MaturityWords();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(?<d>\d{1,2})\s+(?<m>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|June?|July?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(?<y>\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DayMonthYear();

    [GeneratedRegex(@"\b(?<m>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|June?|July?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(?<d>\d{1,2}),?\s+(?<y>\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayYear();

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}/\d{4}\b")]
    private static partial Regex SlashDate();

    [GeneratedRegex(@"\b\d+\s*(?:years?|months?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeTerm();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
