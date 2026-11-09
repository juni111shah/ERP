using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ApproveLeaveRequestHandler(ILeaveRequestRepository requests, ILeaveTypeRepository types, LeaveRequestWorkflow leave, IUnitOfWork uow) : ICommandHandler<ApproveLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(ApproveLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The caller may decide, and the request is waiting for a decision.
        var r = await requests.GetForUpdateAsync(command.Id, token);
        await leave.RequireCanDecideAsync(r, token);
        if (r.Status != LeaveStatus.Submitted) throw new ConflictException($"Only a submitted request can be approved (this one is {r.Status.ToString().ToLowerInvariant()}).");

        // 2. Check the balance again, approve, save and tell the requester.
        var type = await types.GetAsync(r.LeaveTypeId, token);
        await leave.ValidateAsync(r, type, ignorePending: true, token);
        await leave.ApproveCoreAsync(r, type, command.Comment, token);
        await requests.SaveChangesAsync(token);
        await leave.NotifyAsync(r, "approved", token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
