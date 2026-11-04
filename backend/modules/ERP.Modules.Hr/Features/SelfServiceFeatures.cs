using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

// Self-service ("my ..."): any signed-in user whose account is linked to an employee. The employee always comes from the link, never from the request body.
// None of these requests carries a permission: the link is the authorisation, and every query is narrowed to that one employee.

public sealed record MyLeaveInput(Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, string? Reason = null, bool Submit = false);
public sealed record PayslipDto(Guid LineId, Guid RunId, string RunNumber, int Year, int Month, PayrollStatus Status, DateOnly? PaidOn, string EmployeeName, string? EmployeeNumber,
    decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal Adjustment, string? AdjustmentNote, int AbsentDays, decimal OvertimeHours,
    decimal GrossEarnings, decimal Deductions, decimal NetPay);

public sealed record GetMyEmployee() : IQuery<EmployeeDto>;
public sealed record ListMyLeave(PagedQuery Paging) : IQuery<PagedResult<LeaveRequestRow>>;
public sealed record CreateMyLeave(MyLeaveInput Input) : ICommand<LeaveRequestDto>;
public sealed record SubmitMyLeave(Guid Id) : ICommand<LeaveRequestDto>;
public sealed record CancelMyLeave(Guid Id) : ICommand<LeaveRequestDto>;
public sealed record GetMyBalances(int? Year) : IQuery<IReadOnlyList<LeaveBalanceRow>>;
public sealed record ListMyPayslips() : IQuery<IReadOnlyList<PayslipDto>>;
public sealed record GetMyPayslip(Guid LineId) : IQuery<PayslipDto>;
/// <summary>Leave requests of the people who report to me (so a manager can find what to decide without hr.leave.read).</summary>
public sealed record ListTeamLeave(PagedQuery Paging, string? Status) : IQuery<PagedResult<LeaveRequestRow>>;

public sealed class CreateMyLeaveValidator : AbstractValidator<CreateMyLeave>
{
    public CreateMyLeaveValidator()
    {
        RuleFor(x => x.Input.LeaveTypeId).NotEmpty().OverridePropertyName("Input.LeaveTypeId");
        RuleFor(x => x.Input.Reason).MaximumLength(500).OverridePropertyName("Input.Reason");
        RuleFor(x => x.Input).Must(x => x.ToDate >= x.FromDate).WithName("toDate").WithMessage("The leave must end on or after it starts.");
        RuleFor(x => x.Input).Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 365).WithName("toDate").WithMessage("A single request can cover at most a year.");
    }
}

internal sealed class SelfServiceHandlers(
    IReadRepository<Employee> employees, IReadRepository<LeaveRequest> requests, IReadRepository<PayrollRun> runs, ICurrentUser user, EmployeeDtoFactory dtos, IServiceProvider services, IClock clock) :
    IRequestHandler<GetMyEmployee, EmployeeDto>,
    IRequestHandler<ListMyLeave, PagedResult<LeaveRequestRow>>,
    IRequestHandler<CreateMyLeave, LeaveRequestDto>,
    IRequestHandler<SubmitMyLeave, LeaveRequestDto>,
    IRequestHandler<CancelMyLeave, LeaveRequestDto>,
    IRequestHandler<GetMyBalances, IReadOnlyList<LeaveBalanceRow>>,
    IRequestHandler<ListMyPayslips, IReadOnlyList<PayslipDto>>,
    IRequestHandler<GetMyPayslip, PayslipDto>,
    IRequestHandler<ListTeamLeave, PagedResult<LeaveRequestRow>>
{
    private LeaveHandlers Leave() => ActivatorUtilities.CreateInstance<LeaveHandlers>(services);

    /// <summary>The employee linked to the signed-in account; 404 when there is none.</summary>
    private async Task<Employee> MeAsync(CancellationToken ct)
    {
        var uid = user.UserId;
        var me = uid is null ? null : await employees.Query(new Specification<Employee>(e => e.UserId == uid)).FirstOrDefaultAsync(ct);
        return me ?? throw new NotFoundException("No employee is linked to your account.");
    }

    public async Task<EmployeeDto> Handle(GetMyEmployee q, CancellationToken ct) => await dtos.BuildAsync(await MeAsync(ct), includePay: true, ct);

    public async Task<PagedResult<LeaveRequestRow>> Handle(ListMyLeave q, CancellationToken ct)
    {
        var me = (await MeAsync(ct)).Id;
        return await requests.PageAsync(q.Paging, r => new LeaveRequestRow(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason),
            new Specification<LeaveRequest>(r => r.EmployeeId == me), ct);
    }

    public async Task<LeaveRequestDto> Handle(CreateMyLeave cmd, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var i = cmd.Input;
        return await Leave().Handle(new CreateLeaveRequest(new LeaveRequestInput(me.Id, i.LeaveTypeId, i.FromDate, i.ToDate, i.Reason, i.Submit)), ct);
    }

    public async Task<LeaveRequestDto> Handle(SubmitMyLeave cmd, CancellationToken ct)
    {
        await RequireMineAsync(cmd.Id, ct);
        return await Leave().Handle(new SubmitLeaveRequest(cmd.Id), ct);
    }

    public async Task<LeaveRequestDto> Handle(CancelMyLeave cmd, CancellationToken ct)
    {
        await RequireMineAsync(cmd.Id, ct);
        return await Leave().Handle(new CancelLeaveRequest(cmd.Id), ct);
    }

    public async Task<IReadOnlyList<LeaveBalanceRow>> Handle(GetMyBalances q, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        return await Leave().Handle(new GetLeaveBalances(q.Year ?? clock.UtcNow.Year, me.Id, null), ct);
    }

    public async Task<IReadOnlyList<PayslipDto>> Handle(ListMyPayslips q, CancellationToken ct)
        => (await PayslipsAsync(null, ct)).OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToList();

    public async Task<PayslipDto> Handle(GetMyPayslip q, CancellationToken ct)
        => (await PayslipsAsync(q.LineId, ct)).FirstOrDefault() ?? throw new NotFoundException("Payslip not found.");

    public async Task<PagedResult<LeaveRequestRow>> Handle(ListTeamLeave q, CancellationToken ct)
    {
        var me = (await MeAsync(ct)).Id;
        var reports = await employees.Query(new Specification<Employee>(e => e.ManagerEmployeeId == me)).Select(e => e.Id).ToListAsync(ct);
        LeaveStatus? status = null;
        if (!string.IsNullOrWhiteSpace(q.Status)) status = Enum.TryParse<LeaveStatus>(q.Status, true, out var parsed) ? parsed : throw new FieldValidationException("status", "Unknown leave status.");
        return await requests.PageAsync(q.Paging, r => new LeaveRequestRow(r.Id, r.EmployeeId, r.EmployeeName, r.LeaveTypeId, r.FromDate, r.ToDate, r.Days, r.Status, r.Reason),
            new Specification<LeaveRequest>(r => reports.Contains(r.EmployeeId) && (status == null || r.Status == status)), ct);
    }

    /// <summary>Someone else's request looks like a missing one, so ids cannot be probed.</summary>
    private async Task RequireMineAsync(Guid requestId, CancellationToken ct)
    {
        var me = (await MeAsync(ct)).Id;
        if (!await requests.AnyAsync(r => r.Id == requestId && r.EmployeeId == me, ct)) throw new NotFoundException("Leave request not found.");
    }

    private async Task<List<PayslipDto>> PayslipsAsync(Guid? lineId, CancellationToken ct)
    {
        var me = (await MeAsync(ct)).Id;
        return await runs.Query(new Specification<PayrollRun>(r => r.Status == PayrollStatus.Posted || r.Status == PayrollStatus.Paid))
            .SelectMany(r => r.Lines.Where(l => l.EmployeeId == me && (lineId == null || l.Id == lineId)), (r, l) => new PayslipDto(l.Id, r.Id, r.Number, r.Year, r.Month, r.Status, r.PaidOn, l.EmployeeName, l.EmployeeNumber,
                l.Basic, l.Allowances, l.Overtime, l.Absence, l.Adjustment, l.AdjustmentNote, l.AbsentDays, l.OvertimeHours, l.GrossEarnings, l.Deductions, l.NetPay))
            .ToListAsync(ct);
    }
}
