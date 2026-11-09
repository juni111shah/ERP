using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Kind OffCycle needs a note; EmployeeIds (optional) limits an off-cycle run; IncludeSalary adds basic, allowances and deductions to the off-cycle lines (otherwise they start at zero for AdjustPayrollLine).</summary>
public sealed record GeneratePayrollInput(Guid LegalEntityId, int Year, int Month, DateOnly? PlannedPayDate, string? Notes, PayrollKind Kind = PayrollKind.Regular, IReadOnlyList<Guid>? EmployeeIds = null, bool IncludeSalary = false);
public sealed record PayrollLineDto(
    Guid Id, Guid EmployeeId, string? EmployeeNumber, string EmployeeName, int AbsentDays, decimal OvertimeHours, decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal Adjustment,
    string? AdjustmentNote, decimal GrossEarnings, decimal Deductions, decimal NetPay, int ProratedDays = 0, DateOnly? PaidOn = null, Guid? PaymentJournalId = null,
    decimal LoanRecovery = 0m, decimal LeaveEncashment = 0m, decimal EndOfService = 0m, decimal StatutoryDeduction = 0m, decimal EmployerCost = 0m);
public sealed record PayrollDto(
    Guid Id, string Number, Guid LegalEntityId, int Year, int Month, DateOnly? PlannedPayDate, PayrollStatus Status, string? Notes, int Employees, decimal GrossEarnings, decimal Deductions, decimal NetPay,
    DateOnly? PaidOn, Guid? ApprovalRequestId, Guid? AccrualJournalId, Guid? PaymentJournalId, IReadOnlyList<PayrollLineDto> Lines, PayrollKind Kind = PayrollKind.Regular, string? VoidReason = null, DateTime? VoidedAt = null, decimal EmployerCost = 0m)
{
    public static PayrollDto From(PayrollRun r) => new(r.Id, r.Number, r.LegalEntityId, r.Year, r.Month, r.PlannedPayDate, r.Status, r.Notes, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay, r.PaidOn,
        r.ApprovalRequestId, r.AccrualJournalId, r.PaymentJournalId,
        r.Lines.OrderBy(l => l.EmployeeNumber).Select(l => new PayrollLineDto(l.Id, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.AbsentDays, l.OvertimeHours, l.Basic, l.Allowances, l.Overtime, l.Absence, l.Adjustment, l.AdjustmentNote,
            l.GrossEarnings, l.Deductions, l.NetPay, l.ProratedDays, l.PaidOn, l.PaymentJournalId, l.LoanRecovery, l.LeaveEncashment, l.EndOfService, l.StatutoryDeduction, l.EmployerCost)).ToList(), r.Kind, r.VoidReason, r.VoidedAt, r.EmployerCost);
}
public sealed record PayrollSummaryDto(Guid Id, string Number, Guid LegalEntityId, int Year, int Month, PayrollStatus Status, int Employees, decimal GrossEarnings, decimal Deductions, decimal NetPay, DateOnly? PaidOn, PayrollKind Kind = PayrollKind.Regular)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PayrollRun, PayrollSummaryDto>> Projection = r => new PayrollSummaryDto(r.Id, r.Number, r.LegalEntityId, r.Year, r.Month, r.Status, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay, r.PaidOn, r.Kind);
}
