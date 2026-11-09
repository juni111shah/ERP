namespace ERP.Modules.Hr.Features;

public sealed record HrCount(string Key, int Count);
public sealed record HrMonthMovement(int Year, int Month, int Joiners, int Leavers);
public sealed record HrMonthCost(int Year, int Month, int Runs, int Employees, decimal Gross, decimal Deductions, decimal Net);
public sealed record HrLeaveByType(string LeaveType, decimal Days);
public sealed record HrMonthAbsence(int Year, int Month, decimal Days);

/// <summary>
/// <c>PayrollCost</c> is null when the caller lacks <c>hr.payroll.read</c>. Headcount and service years are as at <c>To</c>; movements, cost, leave and absence cover <c>From</c> to <c>To</c>.
/// </summary>
public sealed record HrSummaryReportDto(
    DateOnly From, DateOnly To, Guid? LegalEntityId, int Headcount, decimal AverageServiceYears,
    IReadOnlyList<HrCount> ByDepartment, IReadOnlyList<HrCount> ByNationality, IReadOnlyList<HrCount> ByContractType,
    IReadOnlyList<HrMonthMovement> Movements, IReadOnlyList<HrMonthCost>? PayrollCost,
    IReadOnlyList<HrLeaveByType> LeaveDaysByType, decimal AbsenceDays, IReadOnlyList<HrMonthAbsence> AbsenceByMonth);

public sealed record HrOccasion(Guid EmployeeId, string Number, string Employee, DateOnly Date, int DaysAway, int Years);
public sealed record HrOccasionsDto(DateOnly AsOf, int Days, IReadOnlyList<HrOccasion> Birthdays, IReadOnlyList<HrOccasion> Anniversaries);
