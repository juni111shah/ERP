using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateLeaveRequestHandler(ILeaveRequestRepository requests, LeaveRequestWorkflow leave, ICurrentUser user, IUnitOfWork uow) : ICommandHandler<CreateLeaveRequestCommand, LeaveRequestDto>
{
    public Task<LeaveRequestDto> Handle(CreateLeaveRequestCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. A known, active type for an employee the caller may file for.
        var i = command.Input;
        var (employee, type) = await leave.ResolveAsync(i, token);
        leave.RequireCanFileFor(employee);

        // 2. The draft with its day count, submitted when asked, and saved.
        var r = new LeaveRequest { LegalEntityId = employee.LegalEntityId, EmployeeId = employee.Id, EmployeeName = employee.FullName, LeaveTypeId = type.Id, RequestedByUserId = user.UserId };
        await leave.FillAsync(r, i, token);
        requests.Add(r);
        if (i.Submit) await leave.SubmitCoreAsync(r, type, token);
        await requests.SaveChangesAsync(token);
        return await leave.ToDtoAsync(r, token);
    }, ct);
}
