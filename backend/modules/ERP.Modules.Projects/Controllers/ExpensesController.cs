using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Project expenses: recording, approval and write-off.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/expenses")]
public sealed class ExpensesController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary><c>?live=true</c> leaves out the expenses of templates and archived projects.</summary>
    [HttpGet]
    [RequirePermission(P.ExpensesRead)]
    public Task<PagedResult<ExpenseDto>> List([FromQuery] PagedQuery query, bool? live, CancellationToken ct)
        => Mediator.Send(ProjectLists.Expenses(query, live == true), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ExpensesRead)]
    public Task<ExpenseDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetExpenseQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ExpensesManage)]
    public async Task<IActionResult> Create(ExpenseInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateExpenseCommand(input), ct);
        return Created($"/api/v1/m/projects/expenses/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.ExpensesManage)]
    public Task<ExpenseDto> Update(Guid id, ExpenseInput input, CancellationToken ct)
        => Mediator.Send(new UpdateExpenseCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ExpensesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteExpenseCommand(id), ct);
        return NoContent();
    }

    [HttpPost("submit")]
    [RequirePermission(P.ExpensesManage)]
    public async Task<IActionResult> Submit(IdsRequest body, CancellationToken ct)
        => Ok(new { submitted = await Mediator.Send(new SubmitExpensesCommand(body.Ids), ct) });

    [HttpPost("decide")]
    [RequirePermission(P.ExpensesApprove)]
    public async Task<IActionResult> Decide(DecideRequest body, CancellationToken ct)
        => Ok(new { decided = await Mediator.Send(new DecideExpensesCommand(body.Ids, body.Approve, body.Reason), ct) });

    [HttpPost("write-off")]
    [RequirePermission(P.BillingManage)]
    public async Task<IActionResult> WriteOff(IdsRequest body, CancellationToken ct)
        => Ok(new { writtenOff = await Mediator.Send(new WriteOffExpensesCommand(body.Ids), ct) });
}
