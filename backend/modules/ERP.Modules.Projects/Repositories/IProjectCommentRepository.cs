using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for comments on projects and tasks. Implemented in ProjectCommentRepository.cs.</summary>
public interface IProjectCommentRepository : IRepository<ProjectComment>
{
    /// <summary>The comments on one project or task, read-only.</summary>
    Task<List<ProjectComment>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct);
}
