using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record PayslipAmount(string Label, decimal Amount);
public sealed record PayslipDto(
    Guid RunId, Guid LineId, string RunNumber, PayrollStatus RunStatus, string Company, string LegalEntityName, string Currency,
    Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? Designation, string? Department, string? Email, int Year, int Month, DateOnly? PaidOn, int AbsentDays,
    IReadOnlyList<PayslipAmount> Earnings, IReadOnlyList<PayslipAmount> Deductions, decimal TotalEarnings, decimal TotalDeductions, decimal NetPay, decimal YtdGross, decimal YtdNet);
public sealed record PayslipEmailInput(string? To, string? Message);
public sealed record PayslipBulkEmailResult(int Sent, int Skipped, IReadOnlyList<string> SkippedEmployees);
