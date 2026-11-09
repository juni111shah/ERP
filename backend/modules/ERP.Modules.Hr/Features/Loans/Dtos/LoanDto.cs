using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>InstalmentAmount defaults to Principal / Instalments. StartYear/StartMonth is the first payroll month that recovers an instalment.</summary>
public sealed record LoanInput(Guid EmployeeId, LoanKind Kind, decimal Principal, int Instalments, int StartYear, int StartMonth, decimal? InstalmentAmount = null, string? Reason = null);
public sealed record LoanDto(Guid Id, Guid EmployeeId, string EmployeeName, LoanKind Kind, decimal Principal, decimal InstalmentAmount, int StartYear, int StartMonth, int Instalments,
    decimal Remaining, LoanStatus Status, string? Reason, DateOnly? DisbursedOn, Guid? DisbursementJournalId)
{
    public static LoanDto From(EmployeeLoan l, string employeeName) => new(l.Id, l.EmployeeId, employeeName, l.Kind, l.Principal, l.InstalmentAmount, l.StartYear, l.StartMonth, l.Instalments,
        l.Remaining, l.Status, l.Reason, l.DisbursedOn, l.DisbursementJournalId);
}
public sealed record LoanRow(Guid Id, Guid EmployeeId, LoanKind Kind, decimal Principal, decimal Remaining, int StartYear, int StartMonth, int Instalments, LoanStatus Status, DateOnly? DisbursedOn)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<EmployeeLoan, LoanRow>> Projection = l => new LoanRow(l.Id, l.EmployeeId, l.Kind, l.Principal, l.Remaining, l.StartYear, l.StartMonth, l.Instalments, l.Status, l.DisbursedOn);
}
