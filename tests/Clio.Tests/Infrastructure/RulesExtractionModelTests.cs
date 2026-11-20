using Clio.Application;
using Clio.Domain;
using Clio.Infrastructure;
using Xunit;

namespace Clio.Infrastructure.Tests;

public class RulesExtractionModelTests
{
    private static async Task<FieldCandidate> Extract(TermField field, params string[] lines)
    {
        var model = new RulesExtractionModel();
        var result = await model.ExtractAsync(new DocumentText([new DocumentPage(1, lines)]), CancellationToken.None);
        return result.Single(c => c.Field == field);
    }

    [Theory]
    [InlineData("Facility Amount: ZAR 48,500,000", "ZAR 48500000.00")]
    [InlineData("Loan Amount: USD 3,000,000.00", "USD 3000000.00")]
    [InlineData("Facility Amount: R 8.4 million", "ZAR 8400000.00")]
    [InlineData("Facility amount        ZAR 64 000 000", "ZAR 64000000.00")]
    public async Task Loan_amount_is_found_with_high_confidence(string line, string expected)
    {
        var c = await Extract(TermField.LoanAmount, "TERM SHEET", line);
        Assert.Equal(expected, c.Value);
        Assert.True(c.Confidence >= 0.9m, $"confidence {c.Confidence}");
        Assert.Equal(new SourceLocation(1, 2), c.Source);
    }

    [Theory]
    [InlineData("Margin: 2.85% per annum", "2.85")]
    [InlineData("Pricing: 3-month JIBAR plus 245 basis points", "2.45")]
    [InlineData("Margin: 195 bps", "1.95")]
    [InlineData("Margin: SOFR + 3.25%", "3.25")]
    public async Task Margin_is_found_in_percent_or_basis_points(string line, string expected)
    {
        var c = await Extract(TermField.Margin, line);
        Assert.Equal(expected, c.Value);
        Assert.True(c.Confidence >= 0.9m);
    }

    [Theory]
    [InlineData("Final Maturity Date: 30 June 2031", "2031-06-30")]
    [InlineData("Maturity Date: 2030-03-31", "2030-03-31")]
    [InlineData("Maturity Date: June 30, 2031", "2031-06-30")]
    public async Task Maturity_dates_in_common_formats_are_found(string line, string expected)
    {
        var c = await Extract(TermField.Maturity, line);
        Assert.Equal(expected, c.Value);
        Assert.True(c.Confidence >= 0.9m);
    }

    [Fact]
    public async Task Relative_maturity_has_no_value_and_low_confidence()
    {
        var c = await Extract(TermField.Maturity, "Tenor: 5 years from first utilisation");
        Assert.Null(c.Value);
        Assert.True(c.Confidence < 0.9m);
        Assert.NotNull(c.Note);
    }

    [Fact]
    public async Task Prose_and_step_up_margins_fall_below_threshold()
    {
        var prose = await Extract(TermField.Margin, "The loan bears interest at a margin of 2.6% above prime.");
        Assert.Equal("2.60", prose.Value);
        Assert.True(prose.Confidence < 0.9m);
        var stepUp = await Extract(TermField.Margin, "Margin: 2.50% stepping up to 3.00% after year 3");
        Assert.Equal("2.50", stepUp.Value);
        Assert.True(stepUp.Confidence < 0.9m);
    }

    [Fact]
    public async Task Conflicting_values_lower_confidence()
    {
        var c = await Extract(TermField.LoanAmount, "Facility Amount: ZAR 30,000,000", "Facility Amount: ZAR 35,000,000 (amended)");
        Assert.True(c.Confidence < 0.9m);
        Assert.Contains("conflict", c.Note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_field_is_reported_with_zero_confidence()
    {
        var c = await Extract(TermField.Margin, "Interest: fixed at 11.25% per annum");
        Assert.Null(c.Value);
        Assert.Equal(0m, c.Confidence);
    }
}
