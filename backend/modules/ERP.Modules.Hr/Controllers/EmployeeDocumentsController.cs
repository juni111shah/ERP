using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Employee documents (passports, visas, labour cards ...) and their expiry.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/documents")]
public sealed class EmployeeDocumentsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<PagedResult<EmployeeDocumentRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, EmployeeDocumentRow.Projection, P.EmployeesRead, ct);

    [HttpGet("expiring")]
    [RequirePermission(P.EmployeesRead)]
    public Task<IReadOnlyList<ExpiringDocumentDto>> Expiring(int? days, CancellationToken ct) => Mediator.Send(new ListExpiringDocumentsQuery(days ?? 60), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.EmployeesRead)]
    public Task<EmployeeDocumentDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetEmployeeDocumentQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Create(EmployeeDocumentInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateEmployeeDocumentCommand(input), ct);
        return Created($"{Base}/documents/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<EmployeeDocumentDto> Update(Guid id, EmployeeDocumentInput input, CancellationToken ct) => Mediator.Send(new UpdateEmployeeDocumentCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteEmployeeDocumentCommand(id), ct);
        return NoContent();
    }
}
