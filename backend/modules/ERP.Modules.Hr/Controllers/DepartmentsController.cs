using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Departments (the organisation structure).</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/departments")]
public sealed class DepartmentsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<PagedResult<DepartmentDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, DepartmentDto.Projection, P.EmployeesRead, ct);

    [HttpPost]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Create(DepartmentInput input, CancellationToken ct)
        => Created("/api/v1/m/hr/departments", await Mediator.Send(new CreateDepartmentCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<DepartmentDto> Update(Guid id, DepartmentInput input, CancellationToken ct)
        => Mediator.Send(new UpdateDepartmentCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteDepartmentCommand(id), ct);
        return NoContent();
    }
}
