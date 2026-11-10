using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateDepartmentHandler(IDepartmentRepository departments, DepartmentRules rules) : ICommandHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand command, CancellationToken ct)
    {
        // 1. The code is unique.
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (await departments.AnyAsync(d => d.Code == code, ct)) throw new ConflictException($"A department with code '{code}' already exists.");

        // 2. Check the links, fill, add and save.
        var d = new Department();
        await rules.FillAsync(d, command.Input, code, ct);
        departments.Add(d);
        await departments.SaveChangesAsync(ct);
        return DepartmentDto.From(d);
    }
}
