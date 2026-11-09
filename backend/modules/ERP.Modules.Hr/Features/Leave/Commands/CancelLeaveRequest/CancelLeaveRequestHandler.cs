using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CancelLeaveRequestHandler(ILeaveRequestRepository requests, ILeaveBalanceRepository balances, LeavePolicyService policy, LeaveRequestWorkflow leave, IUnitOfWork uow)
    : ICommandHandler<CancelLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(CancelLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Not when it is already rejected or cancelled.
        var r = await requests.GetForUpdateAsync(command.Id, token);
        if (r.Status is LeaveStatus.Rejected or LeaveStatus.Cancelled) throw new ConflictException($"This request is already {r.Status.ToString().ToLowerInvariant()}.");

        // 2. Approved days go back on the balance of each year.
        if (r.Status == LeaveStatus.Approved)
        {
            var (employeeId, typeId) = (r.EmployeeId, r.LeaveTypeId);
            foreach (var (year, days) in await policy.StoredYearDaysAsync(r, token))
            {
                var b = await balances.FindForYearForUpdateAsync(employeeId, typeId, year, token);
                if (b is not null) b.Taken = Math.Max(0, b.Taken - days);
            }
        }

        // 3. Cancel and save.
        r.Status = LeaveStatus.Cancelled;
        await requests.SaveChangesAsync(token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
