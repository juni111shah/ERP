using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateDepartmentHandler(IDepartmentRepository departments, DepartmentRules rules) : ICommandHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed code must still be unique.
        var d = await departments.GetForUpdateAsync(command.Id, ct);
        var code = command.Input.Code.Trim().ToUpperInvariant();
        if (code != d.Code && await departments.AnyAsync(x => x.Code == code && x.Id != d.Id, ct)) throw new ConflictException($"A department with code '{code}' already exists.");

        // 2. Check the links, fill and save.
        await rules.FillAsync(d, command.Input, code, ct);
        await departments.SaveChangesAsync(ct);
        return DepartmentDto.From(d);
    }
}
