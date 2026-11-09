using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateLeaveRequestHandler(ILeaveRequestRepository requests, LeaveRequestWorkflow leave, IUnitOfWork uow) : ICommandHandler<UpdateLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(UpdateLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a draft, for the same employee, filed by someone who may.
        var r = await requests.GetForUpdateAsync(command.Id, token);
        if (r.Status != LeaveStatus.Draft) throw new ConflictException("Only a draft request can be edited. Cancel it and make a new one.");
        var i = command.Input;
        if (i.EmployeeId != r.EmployeeId) throw new FieldValidationException("employeeId", "A request cannot move to another employee.");
        var (owner, type) = await leave.ResolveAsync(i, token);
        leave.RequireCanFileFor(owner);

        // 2. Apply, submit when asked, and save.
        r.LeaveTypeId = type.Id;
        await leave.FillAsync(r, i, token);
        if (i.Submit) await leave.SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
