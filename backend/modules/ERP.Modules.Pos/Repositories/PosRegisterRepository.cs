using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosRegisterRepository(IRepository<PosRegister> inner) : RepositoryBase<PosRegister>(inner), IPosRegisterRepository
{
    public Task<List<PosRegister>> ListActiveByNameAsync(CancellationToken ct)
        => Query(new Specification<PosRegister>(r => r.IsActive)).OrderBy(r => r.Name).ToListAsync(ct);

    public Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct)
        => Query(new Specification<PosRegister>(r => true)).Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
}
