using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Employees: the master record, its history and leaving (termination and final settlement).</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/employees")]
public sealed class EmployeesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<PagedResult<EmployeeSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => Mediator.Send(new ListEmployeesQuery(query), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.EmployeesRead)]
    public Task<EmployeeDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetEmployeeQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Create(EmployeeInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateEmployeeCommand(input), ct);
        return Created($"{Base}/employees/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<EmployeeDto> Update(Guid id, EmployeeInput input, CancellationToken ct) => Mediator.Send(new UpdateEmployeeCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteEmployeeCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/settlement-preview")]
    [RequirePermission(P.PayrollRead)]
    public Task<SettlementPreviewDto> SettlementPreview(Guid id, DateOnly terminationDate, DateOnly? noticeGivenOn, CancellationToken ct)
        => Mediator.Send(new GetSettlementPreviewQuery(id, terminationDate, noticeGivenOn), ct);

    [HttpPost("{id:guid}/terminate")]
    [RequirePermission(P.EmployeesManage)]
    public Task<SettlementPreviewDto> Terminate(Guid id, TerminateInput input, CancellationToken ct) => Mediator.Send(new TerminateEmployeeCommand(id, input), ct);

    [HttpPost("{id:guid}/final-settlement")]
    [RequirePermission(P.PayrollManage)]
    public async Task<IActionResult> CreateFinalSettlement(Guid id, CancellationToken ct)
    {
        var run = await Mediator.Send(new CreateFinalSettlementCommand(id), ct);
        return Created($"{Base}/payroll/{run.Id}", run);
    }

    [HttpGet("{id:guid}/history")]
    [RequirePermission(P.EmployeesRead)]
    public Task<IReadOnlyList<EmployeeHistoryDto>> History(Guid id, CancellationToken ct) => Mediator.Send(new GetEmployeeHistoryQuery(id), ct);

    [HttpPost("{id:guid}/history")]
    [RequirePermission(P.EmployeesManage)]
    public Task<EmployeeHistoryDto> AddNote(Guid id, HistoryNoteInput input, CancellationToken ct) => Mediator.Send(new AddEmployeeNoteCommand(id, input), ct);
}
