using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Units of the properties, their rent history and status (reserve, release, unavailable).</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/units")]
public sealed class UnitsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.UnitsRead)]
    public Task<PagedResult<UnitListItem>> List([FromQuery] ListUnitsQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.UnitsRead)]
    public Task<UnitDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetUnitQuery(id), ct);

    [HttpGet("{id:guid}/rent-history")]
    [RequirePermission(P.UnitsRead)]
    public Task<IReadOnlyList<UnitRentRateDto>> GetRentHistory(Guid id, CancellationToken ct)
        => Mediator.Send(new GetUnitRentHistoryQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.UnitsManage)]
    public async Task<IActionResult> Create(UnitInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateUnitCommand(input), ct);
        return Created($"/api/v1/m/realestate/units/{created.Id}", created);
    }

    [HttpPost("bulk")]
    [RequirePermission(P.UnitsManage)]
    public async Task<IActionResult> BulkCreate(BulkUnitsInput input, CancellationToken ct)
        => Created("/api/v1/m/realestate/units", await Mediator.Send(new BulkCreateUnitsCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.UnitsManage)]
    public Task<UnitDto> Update(Guid id, UnitInput input, CancellationToken ct)
        => Mediator.Send(new UpdateUnitCommand(id, input), ct);

    // Reserve/release need units.reserve, the rest units.manage: the command declares the permission for its action.
    [HttpPost("{id:guid}/status")]
    [PermissionByRequest(typeof(ChangeUnitStatusCommand))]
    public Task<UnitDto> ChangeStatus(Guid id, UnitStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeUnitStatusCommand(id, body.Action, body.Until, body.Reason), ct);

    public sealed record UnitStatusRequest(UnitAction Action, DateOnly? Until = null, string? Reason = null);
}
