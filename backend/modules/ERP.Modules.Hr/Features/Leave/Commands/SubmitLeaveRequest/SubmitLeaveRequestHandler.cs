using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class SubmitLeaveRequestHandler(ILeaveRequestRepository requests, ILeaveTypeRepository types, LeaveRequestWorkflow leave, IUnitOfWork uow) : ICommandHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await requests.GetForUpdateAsync(command.Id, token);
        if (r.Status != LeaveStatus.Draft) throw new ConflictException($"A {r.Status.ToString().ToLowerInvariant()} request cannot be submitted.");
        var type = await types.GetAsync(r.LeaveTypeId, token);
        await leave.SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
