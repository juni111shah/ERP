using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Project milestones.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/milestones")]
public sealed class MilestonesController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary><c>?live=true</c> leaves out the milestones of templates and archived projects.</summary>
    [HttpGet]
    [RequirePermission(P.MilestonesRead)]
    public Task<PagedResult<MilestoneDto>> List([FromQuery] PagedQuery query, bool? live, CancellationToken ct)
        => Mediator.Send(ProjectLists.Milestones(query, live == true), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.MilestonesRead)]
    public Task<MilestoneDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetMilestoneQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.MilestonesManage)]
    public async Task<IActionResult> Create(MilestoneInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateMilestoneCommand(input), ct);
        return Created($"/api/v1/m/projects/milestones/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.MilestonesManage)]
    public Task<MilestoneDto> Update(Guid id, MilestoneInput input, CancellationToken ct)
        => Mediator.Send(new UpdateMilestoneCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.MilestonesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteMilestoneCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/achieve")]
    [RequirePermission(P.MilestonesManage)]
    public Task<MilestoneDto> Achieve(Guid id, AchieveMilestoneRequest body, CancellationToken ct)
        => Mediator.Send(new AchieveMilestoneCommand(id, body.AchievedDate), ct);

    /// <summary>When the milestone was reached; today when left out.</summary>
    public sealed record AchieveMilestoneRequest(DateOnly? AchievedDate);
}
