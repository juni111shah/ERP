namespace ERP.Modules.RealEstate;

/// <summary>One generated instalment (excluding VAT).</summary>
public sealed record ScheduledInstalment(int Sequence, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueDate, decimal Amount);

/// <summary>
/// Rent-schedule rules (spec RE-BR-SCH-01..06): periods follow the payment plan from the start date; each instalment is due on the
/// first day of its period; a short final period is prorated by actual days / 365 (366 when it covers 29 Feb); every instalment is
/// rounded to 2 dp (half away from zero) and the rounding remainder goes to the last one, so the total equals the rent for the term.
/// </summary>
public static class RentSchedule
{
    public static IReadOnlyList<ScheduledInstalment> Generate(DateOnly start, DateOnly end, decimal annualRent, int paymentFrequency, int graceDays = 0)
    {
        if (!Domain.Lease.PaymentPlans.Contains(paymentFrequency)) throw new ArgumentOutOfRangeException(nameof(paymentFrequency), "Payment plan must be 1, 2, 4, 6 or 12.");
        if (annualRent <= 0) throw new ArgumentOutOfRangeException(nameof(annualRent), "Annual rent must be greater than zero.");
        if (graceDays < 0) throw new ArgumentOutOfRangeException(nameof(graceDays));
        var billingStart = start.AddDays(graceDays);
        if (end <= start || billingStart > end) throw new ArgumentException("The billing period must lie inside the lease term.");

        var months = 12 / paymentFrequency;
        var periods = new List<(DateOnly From, DateOnly To, decimal Raw)>();
        for (var i = 0; ; i++)
        {
            var from = billingStart.AddMonths(i * months); // anchored on the start date, so month-end days never drift
            if (from > end) break;
            var nominalTo = billingStart.AddMonths((i + 1) * months).AddDays(-1);
            if (nominalTo <= end)
                periods.Add((from, nominalTo, annualRent * months / 12m));
            else
            {
                var days = end.DayNumber - from.DayNumber + 1;
                periods.Add((from, end, annualRent * days / DaysInYear(from, end)));
            }
        }

        var termRent = Round(periods.Sum(p => p.Raw));
        var result = new List<ScheduledInstalment>(periods.Count);
        var allocated = 0m;
        for (var i = 0; i < periods.Count; i++)
        {
            var (from, to, raw) = periods[i];
            var amount = i == periods.Count - 1 ? termRent - allocated : Round(raw);
            allocated += amount;
            result.Add(new(i + 1, from, to, from, amount));
        }
        return result;
    }

    /// <summary>Rent for the term (sum of the schedule).</summary>
    public static decimal TermRent(IEnumerable<ScheduledInstalment> schedule) => schedule.Sum(s => s.Amount);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal DaysInYear(DateOnly from, DateOnly to)
    {
        for (var year = from.Year; year <= to.Year; year++)
            if (DateTime.IsLeapYear(year) && new DateOnly(year, 2, 29) >= from && new DateOnly(year, 2, 29) <= to) return 366m;
        return 365m;
    }
}
