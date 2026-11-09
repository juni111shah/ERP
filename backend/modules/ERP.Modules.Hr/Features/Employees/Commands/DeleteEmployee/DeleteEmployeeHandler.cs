using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteEmployeeHandler(IRepository<Employee> employees, EmployeePeople people) : ICommandHandler<DeleteEmployeeCommand>
{
    public async Task<Unit> Handle(DeleteEmployeeCommand command, CancellationToken ct)
    {
        // 1. Someone with history must be terminated, not deleted.
        var e = await employees.GetForUpdateAsync(command.Id, ct);
        if (await people.HasHistoryAsync(e.Id, ct)) throw new ConflictException("This employee has payroll, leave, attendance or review history. Terminate the employee instead.");

        // 2. Remove and save.
        employees.Remove(e);
        await employees.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
