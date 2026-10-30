using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Parties;
using ERP.Domain.Parties;

namespace ERP.UnitTests;

public class OrderToCashRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Theory]
    [InlineData("2026-10-30", true)]
    [InlineData("2027-10-30", true)]
    [InlineData("2027-10-31", false)]
    [InlineData("2000-01-01", false)]
    [InlineData("2000-01-02", true)]
    [InlineData("0001-01-01", false)]
    public void Plausible_dates_are_after_2000_and_within_a_year_ahead(string date, bool expected)
        => Assert.Equal(expected, MoneyValidation.IsPlausibleDate(DateOnly.Parse(date), Today));

    [Theory]
    [InlineData("1.2345", 4, true)]
    [InlineData("1.23456", 4, false)]
    [InlineData("100", 4, true)]
    public void Decimal_places_are_limited(string value, int places, bool expected)
        => Assert.Equal(expected, MoneyValidation.HasAtMostDecimals(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), places));

    [Theory]
    [InlineData(PartyRoles.Customer, true)]
    [InlineData(PartyRoles.Customer | PartyRoles.Vendor, true)]
    [InlineData(PartyRoles.None, true)]
    [InlineData(PartyRoles.Vendor, false)]
    [InlineData(PartyRoles.Employee, false)]
    [InlineData(PartyRoles.Other, false)]
    public void Only_customers_or_role_less_parties_can_be_invoiced(PartyRoles roles, bool allowed)
    {
        var party = new Party { Name = "p", Roles = roles };
        if (allowed) CustomerRules.RequireCustomer(party);
        else Assert.Throws<FieldValidationException>(() => CustomerRules.RequireCustomer(party));
    }

    [Theory]
    [InlineData(typeof(CreateInvoice))]
    [InlineData(typeof(UpdateInvoice))]
    [InlineData(typeof(IssueInvoice))]
    [InlineData(typeof(VoidInvoice))]
    [InlineData(typeof(ApplyCreditNote))]
    [InlineData(typeof(CreateReceipt))]
    [InlineData(typeof(AllocateReceipt))]
    [InlineData(typeof(VoidReceipt))]
    public void Commands_that_change_balances_retry_on_a_concurrency_conflict(Type command)
        => Assert.True(typeof(IRetryOnConcurrency).IsAssignableFrom(command), command.Name);
}
