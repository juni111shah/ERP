using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Checks a department's parent, head and business unit and copies the input onto it (create and update).</summary>
public sealed class DepartmentRules(IDepartmentRepository departments, IReadRepository<Employee> employees, IReadRepository<BusinessUnit> units)
{
    public async Task FillAsync(Department d, DepartmentInput i, string code, CancellationToken ct)
    {
        if (i.ParentDepartmentId is { } parent)
        {
            if (parent == d.Id) throw new FieldValidationException("parentDepartmentId", "A department cannot be its own parent.");
            if (!await departments.AnyAsync(x => x.Id == parent, ct)) throw new FieldValidationException("parentDepartmentId", "Parent department not found.");
            var map = await departments.ParentByIdAsync(ct);
            if (Hierarchy.WouldCycle(d.Id, parent, id => map.GetValueOrDefault(id))) throw new ConflictException("That parent is below this department; the hierarchy would loop.");
        }
        if (i.HeadEmployeeId is { } head && !await employees.AnyAsync(e => e.Id == head, ct)) throw new FieldValidationException("headEmployeeId", "Head employee not found.");
        if (i.BusinessUnitId is { } unit && !await units.AnyAsync(u => u.Id == unit, ct)) throw new FieldValidationException("businessUnitId", "Business unit not found.");
        d.Code = code; d.Name = i.Name.Trim(); d.ParentDepartmentId = i.ParentDepartmentId; d.HeadEmployeeId = i.HeadEmployeeId; d.BusinessUnitId = i.BusinessUnitId; d.IsActive = i.IsActive;
    }
}
