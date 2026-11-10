using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>The till: everything here asks only for the right to sell.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos/terminal")]
[RequirePermission(Permissions.OrdersCreate)]
public sealed class TerminalController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public Task<PosTerminalDto> Get(CancellationToken ct)
        => Mediator.Send(new GetPosTerminalQuery(), ct);

    [HttpGet("session")]
    public Task<PosSessionDto?> GetSession(Guid registerId, CancellationToken ct)
        => Mediator.Send(new GetCurrentSessionQuery(registerId), ct);

    [HttpGet("catalog")]
    public Task<PosCatalogPageDto> GetCatalog(Guid? registerId, string? search, string? code, int? page, int? pageSize, CancellationToken ct)
        => Mediator.Send(new GetPosCatalogQuery(registerId, search, code, page ?? 1, pageSize ?? 60), ct);

    [HttpGet("customers")]
    public Task<List<PosCustomerDto>> GetCustomers(string? search, CancellationToken ct)
        => Mediator.Send(new GetPosCustomersQuery(search), ct);

    [HttpGet("held")]
    public Task<List<PosOrderDto>> GetHeld(Guid? registerId, CancellationToken ct)
        => Mediator.Send(new GetHeldOrdersQuery(registerId), ct);
}
