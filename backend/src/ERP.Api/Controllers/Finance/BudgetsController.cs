using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Budgets and the budget-vs-actual report.</summary>
[Route("api/v1/finance/budgets")]
[CapabilityGate("budgets")]
public sealed class BudgetsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.BudgetsRead)]
    public Task<PagedResult<BudgetSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, BudgetSummaryDto.Projection, CorePermissions.BudgetsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.BudgetsRead)]
    public Task<BudgetDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetBudgetQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.BudgetsWrite)]
    public async Task<IActionResult> Create(BudgetInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateBudgetCommand(input), ct);
        return Created($"/api/v1/finance/budgets/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.BudgetsWrite)]
    public Task<BudgetDto> Update(Guid id, BudgetInput input, CancellationToken ct)
        => Mediator.Send(new UpdateBudgetCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.BudgetsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBudgetCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/vs-actual")]
    [RequirePermission(CorePermissions.BudgetsRead)]
    public Task<BudgetVsActualDto> VsActual(Guid id, int? fromMonth, int? toMonth, CancellationToken ct)
        => Mediator.Send(new GetBudgetVsActualQuery(id, fromMonth ?? 1, toMonth ?? 12), ct);
}
