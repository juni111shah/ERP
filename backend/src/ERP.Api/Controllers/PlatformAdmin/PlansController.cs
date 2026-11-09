using ERP.Api.Infrastructure;
using ERP.Application.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The price list (platform console).</summary>
[Route("api/v1/platform-admin/plans")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class PlansController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string ArchiveOrRestore = "{id:guid}/{action:regex(^(archive|restore)$)}";

    [HttpGet]
    public Task<IReadOnlyList<PlanView>> List(CancellationToken ct) => Mediator.Send(new ListPlansQuery(), ct);

    [HttpPost]
    public Task<PlanView> Create(PlanInput input, CancellationToken ct) => Mediator.Send(new CreatePlanCommand(input), ct);

    [HttpPut("{id:guid}")]
    public Task<PlanView> Update(Guid id, PlanInput input, CancellationToken ct) => Mediator.Send(new UpdatePlanCommand(id, input), ct);

    // {action} is the action name in MVC, so the one route is served by one action per value.
    [HttpPost(ArchiveOrRestore)]
    [ActionName("archive")]
    public Task<PlanView> Archive(Guid id, CancellationToken ct) => Mediator.Send(new SetPlanArchivedCommand(id, true), ct);

    [HttpPost(ArchiveOrRestore)]
    [ActionName("restore")]
    public Task<PlanView> Restore(Guid id, CancellationToken ct) => Mediator.Send(new SetPlanArchivedCommand(id, false), ct);
}
