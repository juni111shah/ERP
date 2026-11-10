using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record DepartmentInput(string Code, string Name, Guid? ParentDepartmentId = null, Guid? HeadEmployeeId = null, Guid? BusinessUnitId = null, bool IsActive = true);

public sealed record DepartmentDto(Guid Id, string Code, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId, Guid? BusinessUnitId, bool IsActive)
{
    public static DepartmentDto From(Department d) => new(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId, d.BusinessUnitId, d.IsActive);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Department, DepartmentDto>> Projection = d => new DepartmentDto(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId, d.BusinessUnitId, d.IsActive);
}
