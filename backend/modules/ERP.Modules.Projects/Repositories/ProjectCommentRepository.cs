using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectCommentRepository(IRepository<ProjectComment> inner) : RepositoryBase<ProjectComment>(inner), IProjectCommentRepository
{
    public Task<List<ProjectComment>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct)
        => Query(new Specification<ProjectComment>(c => c.OwnerType == ownerType && c.OwnerId == ownerId)).ToListAsync(ct);
}
