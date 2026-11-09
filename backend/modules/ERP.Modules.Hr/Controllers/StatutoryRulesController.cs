using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Statutory payroll rules (social security, tax ...) per legal entity.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/statutory-rules")]
public sealed class StatutoryRulesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.PayrollRead)]
    public Task<PagedResult<StatutoryRuleRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, StatutoryRuleRow.Projection, P.PayrollRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.PayrollRead)]
    public Task<StatutoryRuleDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetStatutoryRuleQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> Create(StatutoryRuleInput input, CancellationToken ct)
    {
        var r = await Mediator.Send(new CreateStatutoryRuleCommand(input), ct);
        return Created($"{Base}/statutory-rules/{r.Id}", r);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.PayrollManage)]
    public Task<StatutoryRuleDto> Update(Guid id, StatutoryRuleInput input, CancellationToken ct) => Mediator.Send(new UpdateStatutoryRuleCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteStatutoryRuleCommand(id), ct);
        return NoContent();
    }
}
