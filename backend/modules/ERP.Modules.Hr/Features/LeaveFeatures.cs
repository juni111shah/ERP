using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record LeaveTypeInput(string Code, string Name, bool IsPaid, decimal AnnualDays, decimal CarryForwardCap, bool RequiresApproval = true, bool IsActive = true);
public sealed record LeaveTypeDto(Guid Id, string Code, string Name, bool IsPaid, decimal AnnualDays, decimal CarryForwardCap, bool RequiresApproval, bool IsActive);
public sealed record HolidayInput(DateOnly Date, string Name);
public sealed record HolidayDto(Guid Id, DateOnly Date, string Name);
public sealed record LeaveRequestInput(Guid EmployeeId, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, string? Reason = null, bool Submit = false);
public sealed record LeaveDecisionInput(string? Comment);
public sealed record LeaveRequestDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid LeaveTypeId, string LeaveTypeName, bool IsPaid, DateOnly FromDate, DateOnly ToDate, decimal Days, LeaveStatus Status,
    string? Reason, DateTime? SubmittedAt, DateTime? DecidedAt, string? DecisionComment);
public sealed record LeaveRequestRow(Guid Id, Guid EmployeeId, string EmployeeName, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, decimal Days, LeaveStatus Status, string? Reason);
public sealed record LeaveDaysDto(DateOnly From, DateOnly To, int WorkingDays);
public sealed record LeaveBalanceRow(Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid LeaveTypeId, string LeaveTypeCode, string LeaveTypeName, int Year,
    decimal Entitled, decimal Carried, decimal Taken, decimal Pending, decimal Remaining);

public sealed record CreateLeaveType(LeaveTypeInput Input) : ICommand<LeaveTypeDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record UpdateLeaveType(Guid Id, LeaveTypeInput Input) : ICommand<LeaveTypeDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record CreateHoliday(HolidayInput Input) : ICommand<HolidayDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record UpdateHoliday(Guid Id, HolidayInput Input) : ICommand<HolidayDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record DeleteHoliday(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record GetLeaveRequest(Guid Id) : IQuery<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
public sealed record GetLeaveDays(DateOnly From, DateOnly To) : IQuery<LeaveDaysDto>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
public sealed record GetLeaveBalances(int Year, Guid? EmployeeId, Guid? LegalEntityId) : IQuery<IReadOnlyList<LeaveBalanceRow>>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
/// <summary>Creates a draft request, or submits it straight away with Submit = true.</summary>
public sealed record CreateLeaveRequest(LeaveRequestInput Input) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
public sealed record UpdateLeaveRequest(Guid Id, LeaveRequestInput Input) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
public sealed record SubmitLeaveRequest(Guid Id) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
public sealed record CancelLeaveRequest(Guid Id) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
public sealed record ApproveLeaveRequest(Guid Id, string? Comment) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
public sealed record RejectLeaveRequest(Guid Id, string? Comment) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }

public sealed class LeaveTypeInputValidator : AbstractValidator<LeaveTypeInput>
{
    public LeaveTypeInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AnnualDays).InclusiveBetween(0, 366);
        RuleFor(x => x.CarryForwardCap).InclusiveBetween(0, 366);
    }
}
public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveType> { public CreateLeaveTypeValidator() => RuleFor(x => x.Input).SetValidator(new LeaveTypeInputValidator()); }
public sealed class UpdateLeaveTypeValidator : AbstractValidator<UpdateLeaveType> { public UpdateLeaveTypeValidator() => RuleFor(x => x.Input).SetValidator(new LeaveTypeInputValidator()); }
public sealed class HolidayInputValidator : AbstractValidator<HolidayInput> { public HolidayInputValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(150); } }
public sealed class CreateHolidayValidator : AbstractValidator<CreateHoliday> { public CreateHolidayValidator() => RuleFor(x => x.Input).SetValidator(new HolidayInputValidator()); }
public sealed class UpdateHolidayValidator : AbstractValidator<UpdateHoliday> { public UpdateHolidayValidator() => RuleFor(x => x.Input).SetValidator(new HolidayInputValidator()); }

public sealed class LeaveRequestInputValidator : AbstractValidator<LeaveRequestInput>
{
    public LeaveRequestInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.LeaveTypeId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x).Must(x => x.ToDate >= x.FromDate).WithName("toDate").WithMessage("The leave must end on or after it starts.");
        RuleFor(x => x).Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 365).WithName("toDate").WithMessage("A single request can cover at most a year.");
    }
}
public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequest> { public CreateLeaveRequestValidator() => RuleFor(x => x.Input).SetValidator(new LeaveRequestInputValidator()); }
public sealed class UpdateLeaveRequestValidator : AbstractValidator<UpdateLeaveRequest> { public UpdateLeaveRequestValidator() => RuleFor(x => x.Input).SetValidator(new LeaveRequestInputValidator()); }
public sealed class ApproveLeaveRequestValidator : AbstractValidator<ApproveLeaveRequest> { public ApproveLeaveRequestValidator() => RuleFor(x => x.Comment).MaximumLength(500); }
public sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequest> { public RejectLeaveRequestValidator() => RuleFor(x => x.Comment).MaximumLength(500); }

/// <summary>Builds the working-day calendar (weekend setting + public holidays) for a date range.</summary>
public sealed class LeaveCalendarProvider(IReadRepository<PublicHoliday> holidays, ISettingsService settings)
{
    public async Task<LeaveCalendar> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var days = (await holidays.Query(new Specification<PublicHoliday>(h => h.Date >= from && h.Date <= to)).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        return new LeaveCalendar(LeaveCalendar.ParseWeekend(await settings.GetAsync<string>("hr.weekendDays", null, ct)), days);
    }
}

/// <summary>Approved leave turned into attendance days for a month: paid leave and unpaid leave, in working days. Payroll adds the unpaid days to the recorded absences.</summary>
public sealed class LeaveFeed(IReadRepository<LeaveRequest> requests, IReadRepository<LeaveType> types, LeaveCalendarProvider calendars)
{
    public async Task<Dictionary<Guid, (int Paid, int Unpaid)>> MonthAsync(int year, int month, IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (int Paid, int Unpaid)>();
        if (employeeIds.Count == 0) return result;
        var start = new DateOnly(year, month, 1); var end = start.AddMonths(1).AddDays(-1);
        var approved = await requests.Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Approved && r.FromDate <= end && r.ToDate >= start && employeeIds.Contains(r.EmployeeId))).ToListAsync(ct);
        if (approved.Count == 0) return result;
        var paid = await types.Query().ToDictionaryAsync(t => t.Id, t => t.IsPaid, ct);
        var calendar = await calendars.LoadAsync(start, end, ct);
        foreach (var r in approved)
        {
            var days = calendar.Count(r.FromDate > start ? r.FromDate : start, r.ToDate < end ? r.ToDate : end);
            var (p, u) = result.GetValueOrDefault(r.EmployeeId);
            result[r.EmployeeId] = paid.GetValueOrDefault(r.LeaveTypeId, true) ? (p + days, u) : (p, u + days);
        }
        return result;
    }
}

internal sealed class LeaveHandlers(
    IRepository<LeaveType> types, IRepository<LeaveBalance> balances, IRepository<LeaveRequest> requests, IRepository<PublicHoliday> holidays, IReadRepository<Employee> employees,
    IReadRepository<UserRole> userRoles, LeaveCalendarProvider calendars, INotifier notifier, ICurrentUser user, IClock clock, IUnitOfWork uow) :
    IRequestHandler<CreateLeaveType, LeaveTypeDto>,
    IRequestHandler<UpdateLeaveType, LeaveTypeDto>,
    IRequestHandler<CreateHoliday, HolidayDto>,
    IRequestHandler<UpdateHoliday, HolidayDto>,
    IRequestHandler<DeleteHoliday, Unit>,
    IRequestHandler<GetLeaveRequest, LeaveRequestDto>,
    IRequestHandler<GetLeaveDays, LeaveDaysDto>,
    IRequestHandler<GetLeaveBalances, IReadOnlyList<LeaveBalanceRow>>,
    IRequestHandler<CreateLeaveRequest, LeaveRequestDto>,
    IRequestHandler<UpdateLeaveRequest, LeaveRequestDto>,
    IRequestHandler<SubmitLeaveRequest, LeaveRequestDto>,
    IRequestHandler<CancelLeaveRequest, LeaveRequestDto>,
    IRequestHandler<ApproveLeaveRequest, LeaveRequestDto>,
    IRequestHandler<RejectLeaveRequest, LeaveRequestDto>
{
    // ---- types and holidays ------------------------------------------------------------------------------------------

    public async Task<LeaveTypeDto> Handle(CreateLeaveType cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await types.AnyAsync(t => t.Code == code, ct)) throw new ConflictException($"A leave type with code '{code}' already exists.");
        var t = new LeaveType();
        MapType(cmd.Input, t, code);
        types.Add(t);
        await types.SaveChangesAsync(ct);
        return TypeDto(t);
    }

    public async Task<LeaveTypeDto> Handle(UpdateLeaveType cmd, CancellationToken ct)
    {
        var t = await types.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != t.Code && await types.AnyAsync(x => x.Code == code, ct)) throw new ConflictException($"A leave type with code '{code}' already exists.");
        MapType(cmd.Input, t, code);
        await types.SaveChangesAsync(ct);
        return TypeDto(t);
    }

    public async Task<HolidayDto> Handle(CreateHoliday cmd, CancellationToken ct)
    {
        var date = cmd.Input.Date;
        if (await holidays.AnyAsync(h => h.Date == date, ct)) throw new ConflictException($"{date:yyyy-MM-dd} is already a public holiday.");
        var h = new PublicHoliday { Date = date, Name = cmd.Input.Name.Trim() };
        holidays.Add(h);
        await holidays.SaveChangesAsync(ct);
        return new(h.Id, h.Date, h.Name);
    }

    public async Task<HolidayDto> Handle(UpdateHoliday cmd, CancellationToken ct)
    {
        var h = await holidays.GetForUpdateAsync(cmd.Id, ct);
        var date = cmd.Input.Date;
        if (date != h.Date && await holidays.AnyAsync(x => x.Date == date, ct)) throw new ConflictException($"{date:yyyy-MM-dd} is already a public holiday.");
        h.Date = date; h.Name = cmd.Input.Name.Trim();
        await holidays.SaveChangesAsync(ct);
        return new(h.Id, h.Date, h.Name);
    }

    public async Task<Unit> Handle(DeleteHoliday cmd, CancellationToken ct)
    {
        holidays.Remove(await holidays.GetForUpdateAsync(cmd.Id, ct));
        await holidays.SaveChangesAsync(ct);
        return Unit.Value;
    }

    // ---- reads -------------------------------------------------------------------------------------------------------

    public async Task<LeaveRequestDto> Handle(GetLeaveRequest q, CancellationToken ct) => await ToDtoAsync(await requests.GetAsync(q.Id, ct), ct);

    public async Task<LeaveDaysDto> Handle(GetLeaveDays q, CancellationToken ct)
    {
        if (q.To < q.From) throw new FieldValidationException("to", "The period must end on or after it starts.");
        if (q.To.DayNumber - q.From.DayNumber > 365) throw new FieldValidationException("to", "At most a year at a time.");
        var calendar = await calendars.LoadAsync(q.From, q.To, ct);
        return new(q.From, q.To, calendar.Count(q.From, q.To));
    }

    public async Task<IReadOnlyList<LeaveBalanceRow>> Handle(GetLeaveBalances q, CancellationToken ct)
    {
        var start = new DateOnly(q.Year, 1, 1); var end = new DateOnly(q.Year, 12, 31);
        var staff = (await employees.Query(new Specification<Employee>(e => (q.LegalEntityId == null || e.LegalEntityId == q.LegalEntityId) && (q.EmployeeId == null || e.Id == q.EmployeeId))).ToListAsync(ct))
            .Where(e => e.IsPayableIn(start, end)).OrderBy(e => e.Number).ToList();
        var ids = staff.Select(s => s.Id).ToList();
        var kinds = (await types.Query(new Specification<LeaveType>(t => t.IsActive)).ToListAsync(ct)).OrderBy(t => t.Code).ToList();
        var rows = (await balances.Query(new Specification<LeaveBalance>(b => b.Year == q.Year && ids.Contains(b.EmployeeId))).ToListAsync(ct)).ToDictionary(b => (b.EmployeeId, b.LeaveTypeId));
        var pending = (await requests.Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Submitted && r.FromDate >= start && r.FromDate <= end && ids.Contains(r.EmployeeId))).ToListAsync(ct))
            .GroupBy(r => (r.EmployeeId, r.LeaveTypeId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Days));
        var result = new List<LeaveBalanceRow>();
        foreach (var e in staff)
            foreach (var t in kinds)
            {
                rows.TryGetValue((e.Id, t.Id), out var b);
                var entitled = b?.Entitled ?? t.AnnualDays; var carried = b?.Carried ?? 0m; var taken = b?.Taken ?? 0m;
                var wait = pending.GetValueOrDefault((e.Id, t.Id));
                if (b is null && t.AnnualDays == 0 && wait == 0) continue; // nothing tracked for this type
                result.Add(new(e.Id, e.Number, e.FullName, t.Id, t.Code, t.Name, q.Year, entitled, carried, taken, wait, entitled + carried - taken));
            }
        return result;
    }

    // ---- requests ----------------------------------------------------------------------------------------------------

    public Task<LeaveRequestDto> Handle(CreateLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var (employee, type) = await ResolveAsync(i, token);
        var r = new LeaveRequest { LegalEntityId = employee.LegalEntityId, EmployeeId = employee.Id, EmployeeName = employee.FullName, LeaveTypeId = type.Id, RequestedByUserId = user.UserId };
        await FillAsync(r, i, token);
        requests.Add(r);
        if (i.Submit) await SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await ToDtoAsync(r, token);
    }, ct);

    public Task<LeaveRequestDto> Handle(UpdateLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(cmd.Id, token);
        if (r.Status != LeaveStatus.Draft) throw new ConflictException("Only a draft request can be edited. Cancel it and make a new one.");
        var i = cmd.Input;
        if (i.EmployeeId != r.EmployeeId) throw new FieldValidationException("employeeId", "A request cannot move to another employee.");
        var (_, type) = await ResolveAsync(i, token);
        r.LeaveTypeId = type.Id;
        await FillAsync(r, i, token);
        if (i.Submit) await SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await ToDtoAsync(r, token);
    }, ct);

    public Task<LeaveRequestDto> Handle(SubmitLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(cmd.Id, token);
        if (r.Status != LeaveStatus.Draft) throw new ConflictException($"A {r.Status.ToString().ToLowerInvariant()} request cannot be submitted.");
        var type = await types.GetAsync(r.LeaveTypeId, token);
        await SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await ToDtoAsync(r, token);
    }, ct);

    public Task<LeaveRequestDto> Handle(CancelLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(cmd.Id, token);
        if (r.Status is LeaveStatus.Rejected or LeaveStatus.Cancelled) throw new ConflictException($"This request is already {r.Status.ToString().ToLowerInvariant()}.");
        if (r.Status == LeaveStatus.Approved)
        {
            var b = await balances.Tracked(new Specification<LeaveBalance>(x => x.EmployeeId == r.EmployeeId && x.LeaveTypeId == r.LeaveTypeId && x.Year == r.FromDate.Year)).FirstOrDefaultAsync(token);
            if (b is not null) b.Taken = Math.Max(0, b.Taken - r.Days);
        }
        r.Status = LeaveStatus.Cancelled;
        await requests.SaveChangesAsync(token);
        return await ToDtoAsync(r, token);
    }, ct);

    public Task<LeaveRequestDto> Handle(ApproveLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(cmd.Id, token);
        if (r.Status != LeaveStatus.Submitted) throw new ConflictException($"Only a submitted request can be approved (this one is {r.Status.ToString().ToLowerInvariant()}).");
        var type = await types.GetAsync(r.LeaveTypeId, token);
        await ValidateAsync(r, type, ignorePending: true, token);
        await ApproveCoreAsync(r, type, cmd.Comment, token);
        await requests.SaveChangesAsync(token);
        await NotifyAsync(r, "approved", token);
        return await ToDtoAsync(r, token);
    }, ct);

    public Task<LeaveRequestDto> Handle(RejectLeaveRequest cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(cmd.Id, token);
        if (r.Status != LeaveStatus.Submitted) throw new ConflictException($"Only a submitted request can be rejected (this one is {r.Status.ToString().ToLowerInvariant()}).");
        r.Status = LeaveStatus.Rejected; r.DecidedByUserId = user.UserId; r.DecidedAt = clock.UtcNow; r.DecisionComment = cmd.Comment;
        await requests.SaveChangesAsync(token);
        await NotifyAsync(r, "rejected", token);
        return await ToDtoAsync(r, token);
    }, ct);

    // ---- helpers -----------------------------------------------------------------------------------------------------

    private async Task<(Employee Employee, LeaveType Type)> ResolveAsync(LeaveRequestInput i, CancellationToken ct)
    {
        var employee = await employees.FindAsync(i.EmployeeId, ct) ?? throw new FieldValidationException("employeeId", "Employee not found.");
        if (employee.Status == EmployeeStatus.Terminated) throw new ConflictException("A terminated employee cannot take leave.");
        var type = await types.FindAsync(i.LeaveTypeId, ct) ?? throw new FieldValidationException("leaveTypeId", "Leave type not found.");
        if (!type.IsActive) throw new ConflictException($"Leave type {type.Code} is not active.");
        return (employee, type);
    }

    private async Task FillAsync(LeaveRequest r, LeaveRequestInput i, CancellationToken ct)
    {
        r.FromDate = i.FromDate; r.ToDate = i.ToDate; r.Reason = i.Reason;
        await RecountAsync(r, ct);
    }

    private async Task RecountAsync(LeaveRequest r, CancellationToken ct)
    {
        var calendar = await calendars.LoadAsync(r.FromDate, r.ToDate, ct);
        var days = calendar.Count(r.FromDate, r.ToDate);
        if (days == 0) throw new FieldValidationException("toDate", "The period has no working days (weekends and public holidays do not count).");
        r.Days = days;
    }

    private async Task SubmitCoreAsync(LeaveRequest r, LeaveType type, CancellationToken ct)
    {
        await RecountAsync(r, ct); // holidays or the weekend setting may have changed since the draft was made
        await ValidateAsync(r, type, ignorePending: false, ct);
        r.Status = LeaveStatus.Submitted; r.SubmittedAt = clock.UtcNow;
        if (!type.RequiresApproval) { await ApproveCoreAsync(r, type, "Approved automatically (no approval needed for this leave type).", ct); return; }
        foreach (var approver in await UsersWithAsync(P.LeaveApprove, ct))
            await notifier.NotifyUserAsync(approver, "Leave request to approve", $"{r.EmployeeName}: {r.FromDate:yyyy-MM-dd} to {r.ToDate:yyyy-MM-dd} ({r.Days:0.##} days)", "/hr/leave", "info", ct);
    }

    private async Task ApproveCoreAsync(LeaveRequest r, LeaveType type, string? comment, CancellationToken ct)
    {
        if (type.AnnualDays > 0)
        {
            var b = await BalanceAsync(r.EmployeeId, type, r.FromDate.Year, ct);
            b.Taken += r.Days;
        }
        r.Status = LeaveStatus.Approved; r.DecidedByUserId = user.UserId; r.DecidedAt = clock.UtcNow; r.DecisionComment = comment;
    }

    private async Task ValidateAsync(LeaveRequest r, LeaveType type, bool ignorePending, CancellationToken ct)
    {
        var id = r.Id; var employee = r.EmployeeId; var from = r.FromDate; var to = r.ToDate;
        if (await requests.AnyAsync(x => x.EmployeeId == employee && x.Id != id && (x.Status == LeaveStatus.Submitted || x.Status == LeaveStatus.Approved) && x.FromDate <= to && x.ToDate >= from, ct))
            throw new ConflictException("This employee already has leave requested or approved on some of these days.");
        if (type.AnnualDays <= 0) return; // no balance to enforce
        var b = await BalanceAsync(r.EmployeeId, type, r.FromDate.Year, ct);
        var remaining = b.Entitled + b.Carried - b.Taken;
        var year = r.FromDate.Year; var typeId = type.Id;
        var pending = ignorePending ? 0m : (await requests.Query(new Specification<LeaveRequest>(x => x.EmployeeId == employee && x.LeaveTypeId == typeId && x.Id != id && x.Status == LeaveStatus.Submitted && x.FromDate.Year == year)).Select(x => x.Days).ToListAsync(ct)).Sum();
        if (remaining - pending < r.Days)
            throw new ConflictException($"Not enough {type.Name} balance: {remaining - pending:0.##} day(s) left in {year}, {r.Days:0.##} requested.");
    }

    /// <summary>The balance row of an employee, type and year; created from the type's annual days (plus carry-forward from last year) the first time it is needed.</summary>
    private readonly Dictionary<(Guid, Guid, int), LeaveBalance> createdBalances = new(); // rows added in this unit of work are not visible to queries until saved

    private async Task<LeaveBalance> BalanceAsync(Guid employeeId, LeaveType type, int year, CancellationToken ct)
    {
        var typeId = type.Id;
        if (createdBalances.TryGetValue((employeeId, typeId, year), out var made)) return made;
        var b = await balances.Tracked(new Specification<LeaveBalance>(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Year == year)).FirstOrDefaultAsync(ct);
        if (b is not null) return b;
        var carried = 0m;
        if (type.CarryForwardCap > 0)
        {
            var prev = await balances.Query(new Specification<LeaveBalance>(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Year == year - 1)).FirstOrDefaultAsync(ct);
            if (prev is not null) carried = Math.Min(type.CarryForwardCap, Math.Max(0, prev.Entitled + prev.Carried - prev.Taken));
        }
        b = new LeaveBalance { EmployeeId = employeeId, LeaveTypeId = typeId, Year = year, Entitled = type.AnnualDays, Carried = carried };
        balances.Add(b);
        createdBalances[(employeeId, typeId, year)] = b;
        return b;
    }

    private async Task<List<Guid>> UsersWithAsync(string permission, CancellationToken ct)
        => await userRoles.Query(new Specification<UserRole>(ur => ur.Role!.Permissions.Any(p => p.Permission == permission || p.Permission == "*"))).Select(ur => ur.UserId).Distinct().ToListAsync(ct);

    private async Task NotifyAsync(LeaveRequest r, string verb, CancellationToken ct)
    {
        if (r.RequestedByUserId is not { } to) return;
        await notifier.NotifyUserAsync(to, $"Your leave request was {verb}", $"{r.FromDate:yyyy-MM-dd} to {r.ToDate:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(r.DecisionComment) ? "" : $" - {r.DecisionComment}"), "/hr/leave", verb == "approved" ? "success" : "warning", ct);
    }

    private static void MapType(LeaveTypeInput i, LeaveType t, string code)
    {
        t.Code = code; t.Name = i.Name.Trim(); t.IsPaid = i.IsPaid; t.AnnualDays = i.AnnualDays; t.CarryForwardCap = i.CarryForwardCap; t.RequiresApproval = i.RequiresApproval; t.IsActive = i.IsActive;
    }

    private static LeaveTypeDto TypeDto(LeaveType t) => new(t.Id, t.Code, t.Name, t.IsPaid, t.AnnualDays, t.CarryForwardCap, t.RequiresApproval, t.IsActive);

    private async Task<LeaveRequestDto> ToDtoAsync(LeaveRequest r, CancellationToken ct)
    {
        var t = await types.GetAsync(r.LeaveTypeId, ct);
        return new(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, t.Name, t.IsPaid, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason, r.SubmittedAt, r.DecidedAt, r.DecisionComment);
    }
}
