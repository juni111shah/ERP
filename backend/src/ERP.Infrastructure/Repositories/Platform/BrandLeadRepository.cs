using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class BrandLeadRepository(IRepository<BrandLead> leads) : RepositoryBase<BrandLead>(leads), IBrandLeadRepository
{
    public async Task<(List<BrandLead> Rows, int Total)> ListPageAsync(BrandLeadStatus? status, string? search, int page, int size, CancellationToken ct)
    {
        var query = Query();
        if (status is { } s) query = query.Where(l => l.Status == s);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(l => l.Name.Contains(search) || l.Email.Contains(search) || (l.Company != null && l.Company.Contains(search)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(l => l.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, total);
    }
}
