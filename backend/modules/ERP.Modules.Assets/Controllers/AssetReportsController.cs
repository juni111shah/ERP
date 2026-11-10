using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Assets.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Assets.Controllers;

/// <summary>Asset register and depreciation schedule reports.</summary>
[ModuleGate(AssetsModule.Key)]
[Route("api/v1/m/assets/reports")]
public sealed class AssetReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("register")]
    [RequirePermission(Permissions.ReportsRead)]
    public Task<AssetRegisterDto> Register(DateOnly asOf, Guid? legalEntityId, Guid? categoryId, CancellationToken ct)
        => Mediator.Send(new GetAssetRegisterQuery(asOf, legalEntityId, categoryId), ct);

    [HttpGet("schedule")]
    [RequirePermission(Permissions.ReportsRead)]
    public Task<DepreciationForecastDto> Schedule(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetDepreciationScheduleQuery(from, to, legalEntityId), ct);
}
