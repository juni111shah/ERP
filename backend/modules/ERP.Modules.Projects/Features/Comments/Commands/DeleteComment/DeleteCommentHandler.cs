using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteCommentHandler(IProjectCommentRepository comments, ProjectAccess access, ICurrentUser user) : ICommandHandler<DeleteCommentCommand>
{
    public async Task<Unit> Handle(DeleteCommentCommand command, CancellationToken ct)
    {
        var c = await comments.GetForUpdateAsync(command.Id, ct);
        if (c.UserId != user.UserId && !access.Has(P.ProjectsManage)) throw new ForbiddenException("Only the author or a project manager can delete a comment.");
        comments.Remove(c);
        await comments.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
