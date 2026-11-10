using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class RejectLeaveRequestHandler(ILeaveRequestRepository requests, LeaveRequestWorkflow leave, ICurrentUser user, IClock clock, IUnitOfWork uow) : ICommandHandler<RejectLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(RejectLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The caller may decide, and the request is waiting for a decision.
        var r = await requests.GetForUpdateAsync(command.Id, token);
        await leave.RequireCanDecideAsync(r, token);
        if (r.Status != LeaveStatus.Submitted) throw new ConflictException($"Only a submitted request can be rejected (this one is {r.Status.ToString().ToLowerInvariant()}).");

        // 2. Reject, save and tell the requester.
        r.Status = LeaveStatus.Rejected; r.DecidedByUserId = user.UserId; r.DecidedAt = clock.UtcNow; r.DecisionComment = command.Comment;
        await requests.SaveChangesAsync(token);
        await leave.NotifyAsync(r, "rejected", token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
