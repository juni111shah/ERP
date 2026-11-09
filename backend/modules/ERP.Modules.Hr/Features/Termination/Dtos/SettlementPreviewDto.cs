namespace ERP.Modules.Hr.Features;

/// <summary>NoticeGivenOn is when the employee gave (or was given) notice; with it the unserved part of the notice period can be deducted (setting <c>hr.noticeShortfallDeduction</c>). Without it the notice counts as served.</summary>
public sealed record TerminateInput(DateOnly TerminationDate, string? Reason, DateOnly? LastWorkingDay = null, DateOnly? NoticeGivenOn = null);

/// <summary>What the leaver is owed. Salary is the pro-rated final month (0 when a regular run of that month already paid it); the loan recovery is capped at what is left of the pay.</summary>
public sealed record SettlementPreviewDto(
    Guid EmployeeId, string EmployeeName, DateOnly TerminationDate, DateOnly? LastWorkingDay, int Year, int Month,
    decimal FinalBasic, decimal FinalAllowances, bool SalaryAlreadyPaid, decimal LeaveDays, decimal LeaveEncashment, decimal EndOfService, string? GratuityRule,
    decimal RecurringDeductions, decimal NoticeShortfallDays, decimal NoticeShortfallDeduction, decimal LoanBalance, decimal LoanRecovery, decimal UnrecoveredLoan,
    decimal Gross, decimal Deductions, decimal NetPay, Guid? SettlementRunId, IReadOnlyList<string> Notes, decimal StatutoryDeduction = 0m, decimal EmployerCost = 0m);
