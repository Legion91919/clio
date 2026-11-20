using System.Globalization;
using Agentic.Blocks.Core;

namespace Clio.Domain;

/// <summary>
/// The schema every value must pass before it is saved, whoever produced it (model or analyst).
/// Canonical forms: amount <c>"ZAR 48500000.00"</c>, margin <c>"2.85"</c> (percent), maturity <c>"2031-06-30"</c>.
/// </summary>
public static class FieldValueRules
{
    /// <summary>Highest margin the rules accept at all (percent).</summary>
    public const decimal MaxMargin = 15m;

    /// <summary>Validates and normalises a value.</summary>
    public static Result<string> Normalize(TermField field, string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0) return Error.Validation("field.value_required", "value", "A value is required.");
        return field switch
        {
            TermField.LoanAmount => NormalizeAmount(text),
            TermField.Margin => NormalizeMargin(text),
            TermField.Maturity => NormalizeMaturity(text),
            _ => Error.Validation("field.unknown", "field", "Unknown field."),
        };
    }

    private static Result<string> NormalizeAmount(string text)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length != 3 || !parts[0].All(char.IsLetter))
            return Error.Validation("field.amount_format", "value", "Use a currency code and an amount, for example 'ZAR 48500000.00'.");
        var digits = parts[1].Replace(",", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            return Error.Validation("field.amount_positive", "value", "The amount must be a positive number.");
        var currency = parts[0].ToUpperInvariant();
        var money = Money.TryOf(amount, currency);
        if (money.IsFailure) return Error.Validation("field.currency", "value", $"'{currency}' is not a supported ISO 4217 currency.");
        return string.Create(CultureInfo.InvariantCulture, $"{currency} {money.Value.Amount:0.00}");
    }

    private static Result<string> NormalizeMargin(string text)
    {
        var number = text.TrimEnd('%', ' ');
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var margin) || margin <= 0m || margin > MaxMargin)
            return Error.Validation("field.margin_range", "value", $"Margin must be a percentage above 0 and at most {MaxMargin}.");
        return Math.Round(margin, 4).ToString("0.00##", CultureInfo.InvariantCulture);
    }

    private static Result<string> NormalizeMaturity(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Error.Validation("field.date_format", "value", "Use an ISO date, for example 2031-06-30.");
}
