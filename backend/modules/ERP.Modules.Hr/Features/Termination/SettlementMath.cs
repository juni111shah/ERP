namespace ERP.Modules.Hr.Features;

/// <summary>Pure settlement arithmetic, kept apart from the database so it can be tested on its own.</summary>
public static class SettlementMath
{
    public static decimal DailyRate(decimal basic, int daysBasis) => daysBasis <= 0 ? 0m : basic / daysBasis;

    /// <summary>Unused leave to pay: what the employee has earned so far this year (all of it, or month by month for monthly accrual) plus carried days, less days taken.</summary>
    public static decimal EncashableDays(decimal entitled, decimal carried, decimal taken, bool monthlyAccrual, int terminationMonth)
    {
        var earned = monthlyAccrual ? LeaveEntitlement.Accrued(entitled, terminationMonth) : entitled;
        return Math.Max(0m, earned + carried - taken);
    }

    /// <summary>Days of the notice period that were not served between the day notice was given and the termination date.</summary>
    public static int NoticeShortfallDays(int? noticePeriodDays, DateOnly? noticeGivenOn, DateOnly terminationDate)
    {
        if (noticePeriodDays is not > 0 || noticeGivenOn is not { } given) return 0;
        var served = Math.Max(0, terminationDate.DayNumber - given.DayNumber);
        return Math.Max(0, noticePeriodDays.Value - served);
    }

    /// <summary>The statutory share of the employee never takes more than what is left of the pay.</summary>
    public static decimal StatutoryShare(decimal employeeAmount, decimal payLeft) => Math.Min(employeeAmount, Math.Max(0m, payLeft));

    /// <summary>The loan balance is recovered from what is left of the pay, never more than the balance.</summary>
    public static decimal LoanRecovery(decimal loanBalance, decimal payBeforeLoans) => Math.Max(0m, Math.Min(loanBalance, payBeforeLoans));
}
