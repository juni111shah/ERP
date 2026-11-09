using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class EmployeeDocumentRepository(IRepository<EmployeeDocument> inner) : RepositoryBase<EmployeeDocument>(inner), IEmployeeDocumentRepository
{
    public Task<List<EmployeeDocument>> ListExpiringByAsync(DateOnly limit, CancellationToken ct)
        => Query(new Specification<EmployeeDocument>(d => d.ExpiryDate != null && d.ExpiryDate <= limit)).ToListAsync(ct);
}
