using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record LeaveRequestInput(Guid EmployeeId, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, string? Reason = null, bool Submit = false, bool StartHalfDay = false, bool EndHalfDay = false);
public sealed record LeaveDecisionInput(string? Comment);
public sealed record LeaveRequestDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid LeaveTypeId, string LeaveTypeName, bool IsPaid, DateOnly FromDate, DateOnly ToDate, decimal Days, LeaveStatus Status,
    string? Reason, DateTime? SubmittedAt, DateTime? DecidedAt, string? DecisionComment, bool StartHalfDay = false, bool EndHalfDay = false);
public sealed record LeaveRequestRow(Guid Id, Guid EmployeeId, string EmployeeName, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, decimal Days, LeaveStatus Status, string? Reason)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<LeaveRequest, LeaveRequestRow>> Projection = r => new LeaveRequestRow(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason);
}
public sealed record LeaveDaysDto(DateOnly From, DateOnly To, int WorkingDays);
public sealed record LeaveBalanceRow(Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid LeaveTypeId, string LeaveTypeCode, string LeaveTypeName, int Year,
    decimal Entitled, decimal Carried, decimal Taken, decimal Pending, decimal Remaining);
public sealed record LeaveDefaultsResult(string CountryCode, int Year, IReadOnlyList<string> LeaveTypesAdded, IReadOnlyList<HolidayDto> HolidaysAdded);
