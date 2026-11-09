using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The comments on a project or a task, oldest first.</summary>
public sealed record ListCommentsQuery(string OwnerType, Guid OwnerId) : IQuery<IReadOnlyList<CommentDto>>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
