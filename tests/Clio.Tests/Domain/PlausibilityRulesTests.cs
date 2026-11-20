using Clio.Domain;
using Xunit;

namespace Clio.Domain.Tests;

public class PlausibilityRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    [Fact]
    public void Maturity_in_the_past_is_capped_below_threshold()
    {
        var checkedCandidate = PlausibilityRules.Check(new FieldCandidate(TermField.Maturity, "2019-06-30", 0.96m, new SourceLocation(1, 1)), Today);
        Assert.True(checkedCandidate.Confidence <= 0.5m);
        Assert.Contains("past", checkedCandidate.Note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Margin_above_ten_percent_is_capped()
    {
        var c = PlausibilityRules.Check(new FieldCandidate(TermField.Margin, "11.25", 0.95m, null), Today);
        Assert.True(c.Confidence <= 0.5m);
    }

    [Fact]
    public void Plausible_values_are_unchanged()
    {
        var input = new FieldCandidate(TermField.Maturity, "2031-06-30", 0.96m, null);
        Assert.Equal(input, PlausibilityRules.Check(input, Today));
    }
}
