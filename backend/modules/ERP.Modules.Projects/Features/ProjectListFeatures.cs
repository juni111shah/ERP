using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// A paged list that respects the members-only setting: with "projects.restrictToMembers" on, people without the manage permission see only the rows of
/// projects they manage or belong to. Search, filters, sort and paging are the generic ones.
/// </summary>
public sealed record ScopedProjectList<T, TDto>(PagedQuery Paging, Expression<Func<T, TDto>> Projection, string Permission, Func<IReadOnlyCollection<Guid>, Expression<Func<T, bool>>> Scope)
    : IListRequest<PagedResult<TDto>>, IQuery<PagedResult<TDto>>, IRequirePermission where T : ERP.Domain.Common.Entity
{
    public string RequiredPermission => Permission;

    public async Task<PagedResult<TDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var repo = services.GetRequiredService<IReadRepository<T>>();
        var user = services.GetRequiredService<ICurrentUser>();
        var ids = await VisibleProjectIdsAsync(services, user, ct);
        return await repo.PageAsync(Paging, Projection, ids is null ? null : new Specification<T>(Scope(ids)), ct);
    }

    /// <summary>Null = everything is visible.</summary>
    public static async Task<IReadOnlyCollection<Guid>?> VisibleProjectIdsAsync(IServiceProvider services, ICurrentUser user, CancellationToken ct)
    {
        if (user.PortalKind != null) return null;
        var settings = services.GetRequiredService<ISettingsService>();
        if (await settings.GetAsync<bool?>("projects.restrictToMembers", null, ct) != true) return null;
        if (user.Permissions.Contains(P.ProjectsManage) || user.Permissions.Contains("*")) return null;
        var me = user.UserId ?? Guid.Empty;
        var members = services.GetRequiredService<IReadRepository<ProjectMember>>();
        var projects = services.GetRequiredService<IReadRepository<Project>>();
        var viaTeam = await members.Query(new Specification<ProjectMember>(m => m.UserId == me)).Select(m => m.ProjectId).ToListAsync(ct);
        var managed = await projects.Query(new Specification<Project>(p => p.ManagerUserId == me)).Select(p => p.Id).ToListAsync(ct);
        return viaTeam.Concat(managed).Distinct().ToList();
    }
}
