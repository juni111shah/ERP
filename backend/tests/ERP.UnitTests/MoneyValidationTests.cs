using ERP.Application.Finance;

namespace ERP.UnitTests;

public class MoneyValidationTests
{
    [Theory]
    [InlineData("10", true)]
    [InlineData("10.5", true)]
    [InlineData("10.50", true)]
    [InlineData("0.01", true)]
    [InlineData("10.005", false)]
    [InlineData("33.333", false)]
    [InlineData("0.001", false)]
    public void Only_amounts_to_the_cent_are_money(string value, bool expected)
        => Assert.Equal(expected, MoneyValidation.IsCents(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
}
