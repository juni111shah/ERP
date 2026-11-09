using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>A project's budget by category.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects")]
public sealed class BudgetLinesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("projects/{id:guid}/budget-lines")]
    [RequirePermission(P.ProjectsRead)]
    public Task<IReadOnlyList<BudgetLineDto>> List(Guid id, CancellationToken ct)
        => Mediator.Send(new ListBudgetLinesQuery(id), ct);

    [HttpPost("budget-lines")]
    [RequirePermission(P.ProjectsManage)]
    public async Task<IActionResult> Create(BudgetLineInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/budget-lines", await Mediator.Send(new SaveBudgetLineCommand(null, input), ct));

    [HttpPut("budget-lines/{id:guid}")]
    [RequirePermission(P.ProjectsManage)]
    public Task<BudgetLineDto> Update(Guid id, BudgetLineInput input, CancellationToken ct)
        => Mediator.Send(new SaveBudgetLineCommand(id, input), ct);

    [HttpDelete("budget-lines/{id:guid}")]
    [RequirePermission(P.ProjectsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBudgetLineCommand(id), ct);
        return NoContent();
    }
}
