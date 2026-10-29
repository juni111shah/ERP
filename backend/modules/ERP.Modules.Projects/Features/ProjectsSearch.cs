using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed class ProjectsSearch(
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => ProjectsModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(P.ProjectsRead))
        {
            var pList = await projects.Query(new Specification<Project>(p => p.Name.Contains(term) || p.Number.Contains(term) || (p.CustomerName != null && p.CustomerName.Contains(term))))
                .Take(limit)
                .Select(p => new { p.Id, p.Number, p.Name, p.Status })
                .ToListAsync(ct);

            hits.AddRange(pList.Select(p => new SearchHit("Projects", p.Name, $"{p.Number} · {p.Status}", $"/projects/{p.Id}")));
        }

        if (user.HasPermission(P.TasksRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var tList = await tasks.Query(new Specification<ProjectTask>(t => t.Title.Contains(term) || t.Number.Contains(term)))
                .Take(remaining)
                .Select(t => new { t.Number, t.Title, t.Status, t.ProjectId })
                .ToListAsync(ct);

            hits.AddRange(tList.Select(t => new SearchHit("Project Tasks", t.Title, $"{t.Number} · {t.Status}", $"/projects/{t.ProjectId}")));
        }

        return hits;
    }
}
