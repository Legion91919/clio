using Clio.Domain;
using Xunit;

namespace Clio.Domain.Tests;

public class FieldValueRulesTests
{
    [Theory]
    [InlineData("ZAR 48,500,000", "ZAR 48500000.00")]
    [InlineData("zar 48500000.5", "ZAR 48500000.50")]
    [InlineData("USD 3 000 000.00", "USD 3000000.00")]
    public void Loan_amount_is_normalised_to_currency_and_two_decimals(string input, string expected)
    {
        var result = FieldValueRules.Normalize(TermField.LoanAmount, input);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("48500000")]
    [InlineData("XYZ 100")]
    [InlineData("ZAR 0")]
    [InlineData("ZAR -5")]
    [InlineData("")]
    public void Invalid_loan_amounts_are_rejected(string input) =>
        Assert.False(FieldValueRules.Normalize(TermField.LoanAmount, input).IsSuccess);

    [Theory]
    [InlineData("2.85", "2.85")]
    [InlineData("3.1%", "3.10")]
    [InlineData("5.125", "5.125")]
    public void Margin_is_a_percentage_with_at_least_two_decimals(string input, string expected) =>
        Assert.Equal(expected, FieldValueRules.Normalize(TermField.Margin, input).Value);

    [Theory]
    [InlineData("0")]
    [InlineData("25")]
    [InlineData("abc")]
    public void Implausible_margins_are_rejected(string input) =>
        Assert.False(FieldValueRules.Normalize(TermField.Margin, input).IsSuccess);

    [Fact]
    public void Maturity_must_be_an_iso_date()
    {
        Assert.Equal("2031-06-30", FieldValueRules.Normalize(TermField.Maturity, "2031-06-30").Value);
        Assert.False(FieldValueRules.Normalize(TermField.Maturity, "30/06/2031").IsSuccess);
    }
}
