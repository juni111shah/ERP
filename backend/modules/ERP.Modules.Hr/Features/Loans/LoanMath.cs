using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>The instalment schedule: the recovery of one run never exceeds the balance or the pay that is left.</summary>
public static class LoanMath
{
    public static decimal DefaultInstalment(decimal principal, int instalments) => PayrollMath.R(principal / Math.Max(1, instalments));

    /// <summary>This month's recovery: the instalment, but never more than the balance left, and never more than <paramref name="availablePay"/> (what the employee can still lose without going below zero).</summary>
    public static decimal Recovery(decimal instalment, decimal remaining, decimal availablePay)
        => Math.Max(0m, Math.Min(Math.Min(instalment, remaining), availablePay));

    public static bool Started(EmployeeLoan l, int year, int month) => l.StartYear * 12 + l.StartMonth <= year * 12 + month;
}
