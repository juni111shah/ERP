using ERP.Application.Abstractions;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>Tenant-scoped existence check that ignores soft-delete and legal-entity selection (matches what the FK enforces).</summary>
public sealed class ReferenceCheck(AppDbContext db) : IReferenceCheck
{
    public async Task<bool> AllExistAsync<T>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) where T : OrgEntity
    {
        if (ids.Count == 0) return true;
        var distinct = ids.Distinct().ToList();
        var orgId = db.CurrentOrgId;
        var found = await db.Set<T>().IgnoreQueryFilters() // allow-listed: FK existence check, still tenant-scoped below
            .Where(e => e.OrgId == orgId && distinct.Contains(e.Id)).CountAsync(ct);
        return found == distinct.Count;
    }
}
