namespace ERP.Modules.RealEstate;

public sealed record ScheduledPayment(int Sequence, DateOnly DueDate, decimal Principal, decimal Interest, decimal Total, decimal BalanceAfter);

/// <summary>
/// Amortisation of a funding source: level monthly payments for an interest-bearing loan (annuity), equal principal for a 0% plan.
/// Every figure is rounded to 2 dp; the last payment clears whatever balance is left so the principal always adds up exactly.
/// </summary>
public static class FundingSchedule
{
    public static decimal MonthlyPayment(decimal principal, decimal annualRatePercent, int termMonths)
    {
        if (principal <= 0) throw new ArgumentOutOfRangeException(nameof(principal));
        if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
        if (annualRatePercent <= 0) return Round(principal / termMonths);
        var r = (double)annualRatePercent / 100d / 12d;
        var payment = (double)principal * r / (1d - Math.Pow(1d + r, -termMonths));
        return Round((decimal)payment);
    }

    public static IReadOnlyList<ScheduledPayment> Generate(decimal principal, decimal annualRatePercent, int termMonths, DateOnly firstDue)
    {
        var payment = MonthlyPayment(principal, annualRatePercent, termMonths);
        var monthly = (decimal)((double)annualRatePercent / 100d / 12d);
        var balance = principal;
        var result = new List<ScheduledPayment>(termMonths);
        for (var i = 1; i <= termMonths; i++)
        {
            var interest = annualRatePercent <= 0 ? 0m : Round(balance * monthly);
            var due = firstDue.AddMonths(i - 1);
            decimal principalPart;
            if (i == termMonths) principalPart = balance;                               // the last one clears the balance
            else principalPart = annualRatePercent <= 0 ? payment : Math.Min(payment - interest, balance);
            balance -= principalPart;
            result.Add(new ScheduledPayment(i, due, principalPart, interest, principalPart + interest, balance));
        }
        return result;
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
