using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>Shifts and their cash drawers.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos/sessions")]
public sealed class SessionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.SessionsRead)]
    public Task<PagedResult<PosSessionSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PosSessionSummaryDto.Projection, Permissions.SessionsRead, ct);

    [HttpGet("current")]
    [RequirePermission(Permissions.SessionsRead)]
    public Task<PosSessionDto?> GetCurrent(Guid registerId, CancellationToken ct)
        => Mediator.Send(new GetCurrentSessionQuery(registerId), ct);

    [HttpGet("{id:guid}/report")]
    [RequirePermission(Permissions.SessionsRead)]
    public Task<PosShiftReportDto> GetReport(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPosShiftReportQuery(id), ct);

    [HttpPost("open")]
    [RequirePermission(Permissions.SessionsManage)]
    public async Task<IActionResult> Open(OpenSessionCommand command, CancellationToken ct)
    {
        var opened = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/pos/sessions/{opened.Id}", opened);
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permissions.SessionsManage)]
    public Task<PosSessionDto> Close(Guid id, CloseSessionCommand command, CancellationToken ct)
        => Mediator.Send(command with { SessionId = id }, ct);

    [HttpPost("{id:guid}/drawer-transaction")]
    [RequirePermission(Permissions.SessionsManage)]
    public Task<CashDrawerTransactionDto> RecordDrawerTransaction(Guid id, RecordDrawerTransactionCommand command, CancellationToken ct)
        => Mediator.Send(command with { SessionId = id }, ct);
}
