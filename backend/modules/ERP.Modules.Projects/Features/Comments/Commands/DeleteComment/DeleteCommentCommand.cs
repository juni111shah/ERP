using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a comment; only its author or a project manager may.</summary>
public sealed record DeleteCommentCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
