using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteDepartmentHandler(IDepartmentRepository departments, IReadRepository<Employee> employees) : ICommandHandler<DeleteDepartmentCommand>
{
    public async Task<Unit> Handle(DeleteDepartmentCommand command, CancellationToken ct)
    {
        // 1. Only a department without employees and sub-departments can go.
        var d = await departments.GetForUpdateAsync(command.Id, ct);
        var id = d.Id;
        if (await employees.AnyAsync(e => e.DepartmentId == id, ct)) throw new ConflictException("Employees belong to this department. Move them first.");
        if (await departments.AnyAsync(x => x.ParentDepartmentId == id, ct)) throw new ConflictException("This department has sub-departments. Move or delete them first.");

        // 2. Remove and save.
        departments.Remove(d);
        await departments.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
