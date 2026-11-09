using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Comments on projects and tasks.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/comments")]
public sealed class CommentsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ProjectsRead)]
    public Task<IReadOnlyList<CommentDto>> List(string ownerType, Guid ownerId, CancellationToken ct)
        => Mediator.Send(new ListCommentsQuery(ownerType, ownerId), ct);

    [HttpPost]
    [RequirePermission(P.ProjectsRead)]
    public async Task<IActionResult> Add(CommentInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/comments", await Mediator.Send(new AddCommentCommand(input), ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ProjectsRead)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteCommentCommand(id), ct);
        return NoContent();
    }
}
