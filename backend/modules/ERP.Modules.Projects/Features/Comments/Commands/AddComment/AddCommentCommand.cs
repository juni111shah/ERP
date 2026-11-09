using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Adds a comment to a project or a task, signed with the caller's name.</summary>
public sealed record AddCommentCommand(CommentInput Input) : ICommand<CommentDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
