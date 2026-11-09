using ERP.Application.Abstractions;
using ERP.Domain.Parties;
using ERP.Modules.Pos.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosCustomerRepository(IRepository<Party> inner) : RepositoryBase<Party>(inner), IPosCustomerRepository
{
    public async Task<List<PosCustomerDto>> SearchAsync(string? search, int limit, CancellationToken ct)
    {
        var query = Query(new Specification<Party>(p => p.Roles == PartyRoles.None || (p.Roles & PartyRoles.Customer) == PartyRoles.Customer));
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search) || (p.Phone != null && p.Phone.Contains(search)) || (p.TaxNumber != null && p.TaxNumber.Contains(search)));
        return await query.OrderBy(p => p.Name).Take(limit)
            .Select(p => new PosCustomerDto(p.Id, p.Code, p.Name, p.Phone, p.Email, p.TaxNumber)).ToListAsync(ct);
    }
}
