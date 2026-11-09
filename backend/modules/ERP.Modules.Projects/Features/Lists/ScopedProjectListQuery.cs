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
public sealed record ScopedProjectListQuery<T, TDto>(
    PagedQuery Paging,
    Expression<Func<T, TDto>> Projection,
    string Permission,
    Func<IReadOnlyCollection<Guid>, Expression<Func<T, bool>>> Scope,
    Func<IServiceProvider, IReadOnlyList<TDto>, CancellationToken, Task<IReadOnlyList<TDto>>>? Enrich = null,
    bool LiveOnly = false)
    : IListRequest<PagedResult<TDto>>, IQuery<PagedResult<TDto>>, IRequirePermission where T : ERP.Domain.Common.Entity
{
    public string RequiredPermission => Permission;

    public async Task<PagedResult<TDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var repo = services.GetRequiredService<IReadRepository<T>>();
        var ids = await services.GetRequiredService<ProjectAccess>().VisibleIdsAsync(ct);
        if (LiveOnly)
        {
            // templates and archived projects stay out of the work lists; their own project page asks with live off
            var live = await services.GetRequiredService<IReadRepository<Project>>().Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived)).Select(p => p.Id).ToListAsync(ct);
            ids = ids is null ? live : ids.Intersect(live).ToList();
        }
        var page = await repo.PageAsync(Paging, Projection, ids is null ? null : new Specification<T>(Scope(ids)), ct);
        return Enrich is null ? page : page with { Items = await Enrich(services, page.Items, ct) };
    }
}
