using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class TerminateEmployeeHandler(
    IRepository<Employee> employees, ILeaveRequestRepository leaveRequests, ILeaveBalanceRepository balances, LeavePolicyService policy, ILegalEntityGuard guard,
    EmployeeHistoryRecorder history, IUnitOfWork uow, SettlementCalculator settlement) : ICommandHandler<TerminateEmployeeCommand, SettlementPreviewDto>
{
    public Task<SettlementPreviewDto> Handle(TerminateEmployeeCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Load the employee; someone who has left cannot leave again.
        var i = command.Input;
        var e = await employees.GetForUpdateAsync(command.EmployeeId, token);
        await guard.RequireAsync(e.LegalEntityId, token);
        if (e.Status == EmployeeStatus.Terminated && e.TerminationDate is not null) throw new ConflictException("This employee has already left.");
        if (i.TerminationDate < e.JoinDate) throw new FieldValidationException("terminationDate", "The termination date cannot be before the join date.");

        // 2. Terminate, record it and cancel leave after the last day.
        e.Status = EmployeeStatus.Terminated; e.TerminationDate = i.TerminationDate; e.TerminationReason = i.Reason?.Trim(); e.LastWorkingDay = i.LastWorkingDay; e.NoticeGivenOn = i.NoticeGivenOn;
        history.Terminated(e, i.TerminationDate, i.Reason);
        await CancelFutureLeaveAsync(e, i.TerminationDate, token);
        await employees.SaveChangesAsync(token);

        // 3. Answer with the settlement preview.
        return await settlement.BuildAsync(e, i.TerminationDate, i.LastWorkingDay, i.NoticeGivenOn, token);
    }, ct);

    private async Task CancelFutureLeaveAsync(Employee e, DateOnly termination, CancellationToken ct)
    {
        var id = e.Id;
        var open = await leaveRequests.ListOpenStartingAfterForUpdateAsync(id, termination, ct);
        foreach (var r in open)
        {
            if (r.Status == LeaveStatus.Approved)
            {
                var typeId = r.LeaveTypeId;
                foreach (var (year, days) in await policy.StoredYearDaysAsync(r, ct))
                {
                    var b = await balances.FindForYearForUpdateAsync(id, typeId, year, ct);
                    if (b is not null) b.Taken = Math.Max(0, b.Taken - days);
                }
            }
            r.Status = LeaveStatus.Cancelled;
        }
    }
}
