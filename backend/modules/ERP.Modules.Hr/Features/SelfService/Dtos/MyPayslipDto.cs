using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

// Self-service ("my ..."): any signed-in user whose account is linked to an employee. The employee always comes from the link, never from the request body.
// None of these requests carries a permission: the link is the authorisation, and every query is narrowed to that one employee.

public sealed record MyLeaveInput(Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, string? Reason = null, bool Submit = false);
public sealed record MyPayslipDto(Guid LineId, Guid RunId, string RunNumber, int Year, int Month, PayrollStatus Status, DateOnly? PaidOn, string EmployeeName, string? EmployeeNumber,
    decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal Adjustment, string? AdjustmentNote, int AbsentDays, decimal OvertimeHours,
    decimal GrossEarnings, decimal Deductions, decimal NetPay);
