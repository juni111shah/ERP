using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for departments.</summary>
public interface IDepartmentRepository : IRepository<Department>
{
    /// <summary>The parent of every department, by department id.</summary>
    Task<Dictionary<Guid, Guid?>> ParentByIdAsync(CancellationToken ct);

    /// <summary>Every department as an org-chart source row.</summary>
    Task<List<OrgDepartmentSource>> ListOrgSourcesAsync(CancellationToken ct);

    /// <summary>The business unit of each of the departments, by department id.</summary>
    Task<Dictionary<Guid, Guid?>> BusinessUnitByIdAsync(List<Guid> departmentIds, CancellationToken ct);
}
