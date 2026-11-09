using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Manufacturing.Controllers;

/// <summary>Material requirements planning.</summary>
[ModuleGate(ManufacturingModule.Key)]
[Route("api/v1/m/manufacturing/requirements")]
public sealed class MaterialRequirementsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.WorkOrdersRead)]
    public Task<MaterialRequirementsDto> Get(Guid legalEntityId, Guid? warehouseId, Guid? itemId, decimal? quantity, CancellationToken ct)
        => Mediator.Send(new GetMaterialRequirementsQuery(legalEntityId, warehouseId, itemId, quantity), ct);
}
