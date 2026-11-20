namespace Clio.Domain;

/// <summary>The loan terms slice 1 captures.</summary>
public enum TermField
{
    /// <summary>Facility or loan amount with currency.</summary>
    LoanAmount,

    /// <summary>Interest margin over the base rate, in percent.</summary>
    Margin,

    /// <summary>Final maturity date.</summary>
    Maturity,
}

/// <summary>Where a field stands.</summary>
public enum FieldStatus
{
    /// <summary>Confidence met the threshold; saved without a person.</summary>
    AutoAccepted,

    /// <summary>Below the threshold or invalid; waits in the review queue and is not saved.</summary>
    NeedsReview,

    /// <summary>A person approved (and maybe corrected) it; saved.</summary>
    Approved,
}

/// <summary>Lifecycle of an uploaded term sheet.</summary>
public enum DocumentStatus
{
    /// <summary>Stored and queued.</summary>
    Received,

    /// <summary>The worker is reading it.</summary>
    Processing,

    /// <summary>At least one field waits for a person.</summary>
    NeedsReview,

    /// <summary>Every field is saved.</summary>
    Complete,

    /// <summary>No text could be read; the original is kept.</summary>
    Unreadable,

    /// <summary>Extraction crashed; the original is kept.</summary>
    Failed,
}

/// <summary>Page and line (both 1-based) a value came from.</summary>
public sealed record SourceLocation
{
    /// <summary>Creates a location.</summary>
    public SourceLocation(int page, int line)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        Page = page;
        Line = line;
    }

    /// <summary>1-based page.</summary>
    public int Page { get; }

    /// <summary>1-based line on the page.</summary>
    public int Line { get; }
}

/// <summary>What an extraction model proposes for one field. Values are validated before they are trusted.</summary>
/// <param name="Field">The field.</param>
/// <param name="Value">Proposed value (any format the rules can normalise), or null when not found.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Source">Where it was found.</param>
/// <param name="Evidence">The source text the value came from.</param>
/// <param name="Note">Why confidence is low, for the reviewer.</param>
public sealed record FieldCandidate(TermField Field, string? Value, decimal Confidence, SourceLocation? Source, string? Evidence = null, string? Note = null);

/// <summary>Minimum confidence for a field to be saved without a person.</summary>
public sealed record ConfidenceThreshold
{
    /// <summary>Creates a threshold in (0, 1].</summary>
    public ConfidenceThreshold(decimal value)
    {
        if (value is <= 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(value), value, "Threshold must be greater than 0 and at most 1.");
        Value = value;
    }

    /// <summary>The threshold.</summary>
    public decimal Value { get; }

    /// <summary>True when <paramref name="confidence"/> is at or above the threshold.</summary>
    public bool IsMetBy(decimal confidence) => confidence >= Value;
}
