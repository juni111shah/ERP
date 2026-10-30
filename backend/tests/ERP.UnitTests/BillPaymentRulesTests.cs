using ERP.Application.Finance;
using ERP.Application.Common;
using ERP.Domain.Finance;
using FluentValidation.TestHelper;

namespace ERP.UnitTests;

public class BillPaymentRulesTests
{
    private static BillInput Bill(DateOnly billDate, DateOnly? due = null, decimal quantity = 1m, decimal price = 10m)
        => new(Guid.NewGuid(), Guid.NewGuid(), "REF", billDate, due, [new BillLineInput("Line", quantity, price, Guid.NewGuid())]);

    private static PaymentInput Payment(DateOnly date, DateOnly? chequeDate = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), date, SettlementMethod.BankTransfer, Guid.NewGuid(), 10m, null, null, ChequeDate: chequeDate);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2000-01-01")]
    [InlineData("2099-01-01")]
    public void A_bill_date_outside_the_plausible_range_is_rejected(string date)
        => new BillInputValidator().TestValidate(Bill(DateOnly.Parse(date))).ShouldHaveValidationErrorFor(x => x.BillDate);

    [Fact]
    public void Bill_dates_from_2000_01_02_up_to_a_year_ahead_are_accepted()
    {
        var v = new BillInputValidator();
        v.TestValidate(Bill(new DateOnly(2000, 1, 2))).ShouldNotHaveValidationErrorFor(x => x.BillDate);
        v.TestValidate(Bill(Today)).ShouldNotHaveValidationErrorFor(x => x.BillDate);
        v.TestValidate(Bill(Today.AddYears(1))).ShouldNotHaveValidationErrorFor(x => x.BillDate);
        v.TestValidate(Bill(Today.AddYears(1).AddDays(1))).ShouldHaveValidationErrorFor(x => x.BillDate);
    }

    [Fact]
    public void A_due_date_before_the_bill_date_is_rejected_and_the_same_day_is_accepted()
    {
        var v = new BillInputValidator();
        var date = new DateOnly(2026, 6, 10);
        Assert.NotEmpty(v.TestValidate(Bill(date, date.AddDays(-1))).Errors.Where(e => e.PropertyName.Equals("dueDate", StringComparison.OrdinalIgnoreCase)));
        Assert.Empty(v.TestValidate(Bill(date, date)).Errors.Where(e => e.PropertyName.Equals("dueDate", StringComparison.OrdinalIgnoreCase)));
        Assert.Empty(v.TestValidate(Bill(date, null)).Errors);
    }

    [Fact]
    public void A_due_date_before_2000_is_rejected()
        => new BillInputValidator().TestValidate(Bill(new DateOnly(2026, 6, 10), new DateOnly(1999, 12, 31))).ShouldHaveValidationErrorFor(x => x.DueDate);

    [Theory]
    [InlineData("1.00001", "10", false)]
    [InlineData("1", "9.12345", false)]
    [InlineData("1.2345", "9.1234", true)]
    [InlineData("1", "0", true)]
    public void Bill_quantity_and_price_allow_at_most_four_decimals(string quantity, string price, bool accepted)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var result = new BillInputValidator().TestValidate(Bill(new DateOnly(2026, 6, 10), quantity: decimal.Parse(quantity, inv), price: decimal.Parse(price, inv)));
        Assert.Equal(accepted, result.IsValid);
    }

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2099-01-01")]
    public void A_payment_date_outside_the_plausible_range_is_rejected(string date)
        => new PaymentInputValidator().TestValidate(Payment(DateOnly.Parse(date))).ShouldHaveValidationErrorFor(x => x.Date);

    [Fact]
    public void A_cheque_date_in_1999_is_rejected_and_a_post_dated_cheque_within_a_year_is_accepted()
    {
        var v = new PaymentInputValidator();
        v.TestValidate(Payment(new DateOnly(2026, 6, 1), new DateOnly(1999, 12, 31))).ShouldHaveValidationErrorFor(x => x.ChequeDate);
        v.TestValidate(Payment(new DateOnly(2026, 6, 1), Today.AddMonths(6))).ShouldNotHaveValidationErrorFor(x => x.ChequeDate);
        v.TestValidate(Payment(new DateOnly(2026, 6, 1), null)).ShouldNotHaveValidationErrorFor(x => x.ChequeDate);
    }

    [Theory]
    [InlineData("0001-01-01", false)]
    [InlineData("0002-01-01", false)]
    [InlineData("1999-12-31", false)]
    [InlineData("2099-01-01", false)]
    [InlineData("2026-06-10", true)]
    public void The_clearing_date_must_be_plausible(string date, bool accepted)
        => Assert.Equal(accepted, new ClearIssuedChequeValidator().TestValidate(new ClearIssuedCheque(Guid.NewGuid(), DateOnly.Parse(date))).IsValid);

    [Theory]
    [InlineData(typeof(UpdateBill), true)]
    [InlineData(typeof(DeleteBill), true)]
    [InlineData(typeof(PostBill), true)]
    public void Bill_commands_that_change_state_retry_on_a_concurrency_conflict(Type command, bool retryable)
        => Assert.Equal(retryable, typeof(IRetryOnConcurrency).IsAssignableFrom(command));
}
