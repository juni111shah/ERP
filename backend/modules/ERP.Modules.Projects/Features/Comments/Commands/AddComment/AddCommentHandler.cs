using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class AddCommentHandler(
    IProjectCommentRepository comments,
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    ProjectPeople people,
    ProjectAccess access,
    ICurrentUser user)
    : ICommandHandler<AddCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(AddCommentCommand command, CancellationToken ct)
    {
        // 1. The project or task must exist and be one the caller may reach.
        var i = command.Input;
        Guid projectId;
        if (i.OwnerType == "project") { projectId = (await projects.GetAsync(i.OwnerId, ct)).Id; }
        else projectId = (await tasks.GetAsync(i.OwnerId, ct)).ProjectId;
        await access.EnsureProjectAsync(projectId, ct);

        // 2. Sign it with the caller's name and save.
        var author = await people.NameAsync(user.UserId, ct) ?? user.Email ?? "Someone";
        var c = new ProjectComment { OwnerType = i.OwnerType, OwnerId = i.OwnerId, ProjectId = projectId, UserId = user.UserId, AuthorName = author, Text = i.Text.Trim() };
        comments.Add(c);
        await comments.SaveChangesAsync(ct);
        return CommentDto.From(c);
    }
}
