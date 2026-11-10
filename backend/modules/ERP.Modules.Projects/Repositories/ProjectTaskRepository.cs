using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectTaskRepository(IRepository<ProjectTask> inner) : RepositoryBase<ProjectTask>(inner), IProjectTaskRepository
{
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled)).CountAsync(ct);

    public Task<List<Guid>> ListIdsOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => x.ProjectId == projectId)).Select(x => x.Id).ToListAsync(ct);

    public Task<List<string>> ListOpenNumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => ids.Contains(x.Id) && x.Status != ProjectTaskStatus.Done && x.Status != ProjectTaskStatus.Cancelled)).Select(x => x.Number).ToListAsync(ct);

    public Task<int> CountOpenSubtasksAsync(Guid parentId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => x.ParentTaskId == parentId && x.Status != ProjectTaskStatus.Done && x.Status != ProjectTaskStatus.Cancelled)).CountAsync(ct);

    public Task<TaskParentLink?> FindParentLinkAsync(Guid id, Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => x.Id == id && x.ProjectId == projectId)).Select(x => new TaskParentLink(x.Id, x.ParentTaskId)).FirstOrDefaultAsync(ct);

    public Task<string?> GetTitleAsync(Guid id, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => x.Id == id)).Select(x => x.Title).FirstOrDefaultAsync(ct);

    public Task<Dictionary<Guid, string>> MapTitlesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<ProjectTask>(t => ids.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Title, ct);

    public Task<Dictionary<Guid, string>> MapLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<ProjectTask>(t => ids.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Number + " " + t.Title, ct);

    public Task<List<ProjectTask>> ListLiveOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled)).ToListAsync(ct);

    public Task<List<ProjectTask>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTask>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectTask>> ListOfProjectForUpdateAsync(Guid projectId, CancellationToken ct)
        => Tracked(new Specification<ProjectTask>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectTask>> ListOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectTask>(t => projectIds.Contains(t.ProjectId))).ToListAsync(ct);
}
