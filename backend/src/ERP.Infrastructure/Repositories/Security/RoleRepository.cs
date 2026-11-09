using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Security;

internal sealed class RoleRepository(IRepository<Role> roles) : RepositoryBase<Role>(roles), IRoleRepository
{
    public async Task<Role> GetWithPermissionsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<Role>(r => r.Id == id)).Include(r => r.Permissions).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<Guid>> ListExistingIdsAsync(Guid[] ids, CancellationToken ct)
        => Query(new Specification<Role>(r => ids.Contains(r.Id))).Select(r => r.Id).ToListAsync(ct);

    public Task<List<Guid>> ListAdministratorRoleIdsAsync(CancellationToken ct)
        => Query(new Specification<Role>(r => r.Permissions.Any(p => p.Permission == CorePermissions.All))).Select(r => r.Id).ToListAsync(ct);

    public Task<List<string>> ListNamesAsync(CancellationToken ct) => Query().Select(r => r.Name).ToListAsync(ct);
}
