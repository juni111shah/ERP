using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Onboarding and offboarding tasks of employees, and the HR to-do list.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr")]
public sealed class EmployeeTasksController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("employees/{id:guid}/tasks")]
    [RequirePermission(P.EmployeesRead)]
    public Task<IReadOnlyList<EmployeeTaskDto>> ListOfEmployee(Guid id, CancellationToken ct) => Mediator.Send(new ListEmployeeTasksQuery(id), ct);

    [HttpPost("employees/{id:guid}/tasks")]
    [RequirePermission(P.EmployeesManage)]
    public Task<EmployeeTaskDto> Add(Guid id, EmployeeTaskInput input, CancellationToken ct) => Mediator.Send(new AddEmployeeTaskCommand(id, input), ct);

    [HttpPost("employees/{id:guid}/checklists/{templateId:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<IReadOnlyList<EmployeeTaskDto>> ApplyChecklist(Guid id, Guid templateId, CancellationToken ct) => Mediator.Send(new ApplyChecklistCommand(id, templateId), ct);

    [HttpGet("tasks/open")]
    [RequirePermission(P.EmployeesRead)]
    public Task<IReadOnlyList<OpenTaskDto>> ListOpen(CancellationToken ct) => Mediator.Send(new ListOpenTasksQuery(), ct);

    [HttpPost("tasks/{id:guid}/done")]
    [RequirePermission(P.EmployeesManage)]
    public Task<EmployeeTaskDto> SetDone(Guid id, TaskDoneRequest body, CancellationToken ct) => Mediator.Send(new SetEmployeeTaskDoneCommand(id, body.Done), ct);

    [HttpDelete("tasks/{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteEmployeeTaskCommand(id), ct);
        return NoContent();
    }

    /// <summary>Ticks the task off (true) or opens it again (false).</summary>
    public sealed record TaskDoneRequest(bool Done);
}
