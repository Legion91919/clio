using System.Globalization;

namespace Clio.Domain;

/// <summary>
/// Business cross-checks that lower confidence on values that parse but look wrong, so a person sees them.
/// They never change a value; they only route it to review.
/// </summary>
public static class PlausibilityRules
{
    /// <summary>Confidence given to implausible values (below any sensible threshold).</summary>
    public const decimal ImplausibleCap = 0.5m;

    /// <summary>Margins above this are unusual for senior debt and need a person.</summary>
    public const decimal UnusualMargin = 10m;

    /// <summary>Checks one candidate against today's date.</summary>
    public static FieldCandidate Check(FieldCandidate candidate, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Value is null) return candidate;
        var reason = candidate.Field switch
        {
            TermField.Maturity when DateOnly.TryParseExact(candidate.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d <= today
                => "Maturity is in the past.",
            TermField.Margin when decimal.TryParse(candidate.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m) && m > UnusualMargin
                => $"Margin above {UnusualMargin}% is unusual.",
            _ => null,
        };
        return reason is null || candidate.Confidence <= ImplausibleCap
            ? candidate
            : candidate with { Confidence = ImplausibleCap, Note = reason };
    }

    /// <summary>Checks every candidate.</summary>
    public static IReadOnlyList<FieldCandidate> Apply(IEnumerable<FieldCandidate> candidates, DateOnly today) =>
        candidates.Select(c => Check(c, today)).ToList();
}
