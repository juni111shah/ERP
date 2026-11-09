using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Onboarding and offboarding checklist templates.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/checklists")]
public sealed class ChecklistsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<PagedResult<ChecklistTemplateRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, ChecklistTemplateRow.Projection, P.EmployeesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.EmployeesRead)]
    public Task<ChecklistTemplateDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetChecklistTemplateQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Create(ChecklistTemplateInput input, CancellationToken ct)
    {
        var t = await Mediator.Send(new CreateChecklistTemplateCommand(input), ct);
        return Created($"{Base}/checklists/{t.Id}", t);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<ChecklistTemplateDto> Update(Guid id, ChecklistTemplateInput input, CancellationToken ct) => Mediator.Send(new UpdateChecklistTemplateCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteChecklistTemplateCommand(id), ct);
        return NoContent();
    }
}
