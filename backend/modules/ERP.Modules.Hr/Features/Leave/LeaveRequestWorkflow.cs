using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>
/// The rules of a leave request shared by its handlers: who may file and decide, the day count, the balance check,
/// submitting (with notifications) and approving (taking the days off the balance). Registered transient: each handler
/// gets its own instance, like the balance rows it creates in its unit of work.
/// </summary>
public sealed class LeaveRequestWorkflow(
    ILeaveTypeRepository types, ILeaveBalanceRepository balances, ILeaveRequestRepository requests, IReadRepository<Employee> employees,
    IUserRoleRepository userRoles, LeaveCalendarProvider calendars, LeavePolicyService policy, INotifier notifier, ICurrentUser user, IClock clock, ISettingsService settings)
{
    public async Task<(Employee Employee, LeaveType Type)> ResolveAsync(LeaveRequestInput i, CancellationToken ct)
    {
        var employee = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Employee not found.");
        if (employee.Status == EmployeeStatus.Terminated) throw new ConflictException("A terminated employee cannot take leave.");
        var type = await types.FindAsync(i.LeaveTypeId, ct) ?? throw new FieldValidationException("leaveTypeId", "Leave type not found.");
        if (!type.IsActive) throw new ConflictException($"Leave type {type.Code} is not active.");
        return (employee, type);
    }

    public async Task FillAsync(LeaveRequest r, LeaveRequestInput i, CancellationToken ct)
    {
        r.FromDate = i.FromDate; r.ToDate = i.ToDate; r.Reason = i.Reason; r.StartHalfDay = i.StartHalfDay; r.EndHalfDay = i.EndHalfDay;
        await RecountAsync(r, ct);
    }

    private async Task RecountAsync(LeaveRequest r, CancellationToken ct)
    {
        var calendar = await calendars.LoadAsync(r.FromDate, r.ToDate, ct);
        var (_, days, error) = LeaveEntitlement.Split(r.FromDate, r.ToDate, r.StartHalfDay, r.EndHalfDay, calendar.IsWorkingDay);
        if (error is not null) throw new FieldValidationException(r.StartHalfDay && !calendar.IsWorkingDay(r.FromDate) ? "startHalfDay" : "endHalfDay", error);
        if (days == 0) throw new FieldValidationException("toDate", "The period has no working days (weekends and public holidays do not count).");
        r.Days = days;
    }

    public async Task SubmitCoreAsync(LeaveRequest r, LeaveType type, CancellationToken ct)
    {
        await RecountAsync(r, ct); // holidays or the weekend setting may have changed since the draft was made
        await ValidateAsync(r, type, ignorePending: false, ct);
        await policy.RequireAttachmentAsync(r, type, ct);
        r.Status = LeaveStatus.Submitted; r.SubmittedAt = clock.UtcNow;
        if (!type.RequiresApproval) { await ApproveCoreAsync(r, type, "Approved automatically (no approval needed for this leave type).", ct); return; }
        // the line manager (if linked to a user) is told first, then everyone who may approve
        var managerUser = await ManagerUserAsync(r.EmployeeId, ct);
        if (managerUser is { } mu)
            await notifier.NotifyUserAsync(mu, "Leave request to approve", $"{r.EmployeeName}: {r.FromDate:yyyy-MM-dd} to {r.ToDate:yyyy-MM-dd} ({r.Days:0.##} days)", "/hr/leave", "info", ct);
        foreach (var approver in (await userRoles.ListUsersWithPermissionAsync(P.LeaveApprove, ct)).Where(a => a != managerUser))
            await notifier.NotifyUserAsync(approver, "Leave request to approve", $"{r.EmployeeName}: {r.FromDate:yyyy-MM-dd} to {r.ToDate:yyyy-MM-dd} ({r.Days:0.##} days)", "/hr/leave", "info", ct);
    }

    public async Task ApproveCoreAsync(LeaveRequest r, LeaveType type, string? comment, CancellationToken ct)
    {
        if (type.AnnualDays > 0)
        {
            var employee = await employees.GetAsync(r.EmployeeId, ct);
            var split = await SplitAsync(r, ct);
            foreach (var (year, days) in split) (await BalanceAsync(employee, type, year, ct)).Taken += days;
            policy.StoreYearDays(r, split);
        }
        r.Status = LeaveStatus.Approved; r.DecidedByUserId = user.UserId; r.DecidedAt = clock.UtcNow; r.DecisionComment = comment;
    }

    private async Task<Dictionary<int, decimal>> SplitAsync(LeaveRequest r, CancellationToken ct)
    {
        var calendar = await calendars.LoadAsync(r.FromDate, r.ToDate, ct);
        return LeaveEntitlement.Split(r.FromDate, r.ToDate, r.StartHalfDay, r.EndHalfDay, calendar.IsWorkingDay).ByYear;
    }

    public async Task ValidateAsync(LeaveRequest r, LeaveType type, bool ignorePending, CancellationToken ct)
    {
        var id = r.Id; var employeeId = r.EmployeeId; var from = r.FromDate; var to = r.ToDate;
        if (await requests.AnyAsync(x => x.EmployeeId == employeeId && x.Id != id && (x.Status == LeaveStatus.Submitted || x.Status == LeaveStatus.Approved) && x.FromDate <= to && x.ToDate >= from, ct))
            throw new ConflictException("This employee already has leave requested or approved on some of these days.");
        if (type.AnnualDays <= 0) return; // no balance to enforce
        var employee = await employees.GetAsync(employeeId, ct);
        var typeId = type.Id;
        foreach (var (year, days) in await SplitAsync(r, ct)) // the balance check applies per calendar year
        {
            var b = await BalanceAsync(employee, type, year, ct);
            var available = b.Entitled + b.Carried - b.Taken;
            if (type.AccrualMode == LeaveAccrualMode.Monthly)
            {
                var months = LeaveEntitlement.AccruedMonths(year, policy.Today, r.ToDate);
                available = LeaveEntitlement.Accrued(b.Entitled, months) + b.Carried - b.Taken;
            }
            var pending = ignorePending ? 0m : await requests.PendingDaysAsync(employeeId, typeId, id, year, ct);
            if (available - pending < days)
                throw new ConflictException($"Not enough {type.Name} balance: {available - pending:0.##} day(s) available in {year}, {days:0.##} requested.");
        }
    }

    /// <summary>The balance row of an employee, type and year; created from the type's entitlement for the employee's service (plus carry-forward from last year) the first time it is needed.</summary>
    private readonly Dictionary<(Guid, Guid, int), LeaveBalance> createdBalances = new(); // rows added in this unit of work are not visible to queries until saved

    private async Task<LeaveBalance> BalanceAsync(Employee employee, LeaveType type, int year, CancellationToken ct)
    {
        var employeeId = employee.Id; var typeId = type.Id;
        if (createdBalances.TryGetValue((employeeId, typeId, year), out var made)) return made;
        var b = await balances.FindForYearForUpdateAsync(employeeId, typeId, year, ct);
        if (b is not null) return b;
        var carried = 0m;
        if (type.CarryForwardCap > 0)
        {
            var prev = await balances.FindForYearAsync(employeeId, typeId, year - 1, ct);
            if (prev is not null) carried = Math.Min(type.CarryForwardCap, Math.Max(0, prev.Entitled + prev.Carried - prev.Taken));
        }
        b = new LeaveBalance { EmployeeId = employeeId, LeaveTypeId = typeId, Year = year, Entitled = await policy.EntitlementAsync(employee, type, year, ct), Carried = carried };
        balances.Add(b);
        createdBalances[(employeeId, typeId, year)] = b;
        return b;
    }

    /// <summary>Staff without hr.leave.manage can only file leave for the employee linked to their own account.</summary>
    public void RequireCanFileFor(Employee employee)
    {
        if (user.HasPermission(P.LeaveManage)) return;
        if (user.UserId is { } me && employee.UserId == me) return;
        throw new ForbiddenException("You can only file leave for your own employee record.");
    }

    /// <summary>Decisions need hr.leave.approve, or being the line manager of the employee; with hr.blockSelfApproval (default on) nobody decides their own request.</summary>
    public async Task RequireCanDecideAsync(LeaveRequest r, CancellationToken ct)
    {
        var owner = await employees.FindAsync(r.EmployeeId, ct);
        if (!user.HasPermission(P.LeaveApprove))
        {
            var me = user.UserId;
            var manager = owner?.ManagerEmployeeId is { } mid ? await employees.FindAsync(mid, ct) : null;
            if (me is null || manager?.UserId != me) throw new ForbiddenException();
        }
        if (await settings.GetAsync<bool?>("hr.blockSelfApproval", r.LegalEntityId, ct) ?? true)
            if (user.UserId is { } uid && (r.RequestedByUserId == uid || owner?.UserId == uid)) throw new ConflictException("You cannot decide your own leave request.");
    }

    private async Task<Guid?> ManagerUserAsync(Guid employeeId, CancellationToken ct)
    {
        var owner = await employees.FindAsync(employeeId, ct);
        return owner?.ManagerEmployeeId is { } mid ? (await employees.FindAsync(mid, ct))?.UserId : null;
    }

    public async Task NotifyAsync(LeaveRequest r, string verb, CancellationToken ct)
    {
        if (r.RequestedByUserId is not { } to) return;
        await notifier.NotifyUserAsync(to, $"Your leave request was {verb}", $"{r.FromDate:yyyy-MM-dd} to {r.ToDate:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(r.DecisionComment) ? "" : $" - {r.DecisionComment}"), "/hr/leave", verb == "approved" ? "success" : "warning", ct);
    }

    public async Task<LeaveRequestDto> ToDtoAsync(LeaveRequest r, CancellationToken ct)
    {
        var t = await types.GetAsync(r.LeaveTypeId, ct);
        return new(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, t.Name, t.IsPaid, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason, r.SubmittedAt, r.DecidedAt, r.DecisionComment, r.StartHalfDay, r.EndHalfDay);
    }
}
