using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class DepartmentRepository(IRepository<Department> inner) : RepositoryBase<Department>(inner), IDepartmentRepository
{
    public Task<Dictionary<Guid, Guid?>> ParentByIdAsync(CancellationToken ct)
        => Query().Select(x => new { x.Id, x.ParentDepartmentId }).ToDictionaryAsync(x => x.Id, x => x.ParentDepartmentId, ct);

    public Task<List<OrgDepartmentSource>> ListOrgSourcesAsync(CancellationToken ct)
        => Query().Select(d => new OrgDepartmentSource(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.HeadEmployeeId)).ToListAsync(ct);

    public Task<Dictionary<Guid, Guid?>> BusinessUnitByIdAsync(List<Guid> departmentIds, CancellationToken ct)
        => Query(new Specification<Department>(d => departmentIds.Contains(d.Id))).ToDictionaryAsync(d => d.Id, d => d.BusinessUnitId, ct);
}
