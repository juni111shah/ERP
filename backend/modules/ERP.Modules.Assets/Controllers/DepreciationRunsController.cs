using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Assets.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Assets.Controllers;

/// <summary>Monthly depreciation runs.</summary>
[ModuleGate(AssetsModule.Key)]
[Route("api/v1/m/assets/depreciation-runs")]
public sealed class DepreciationRunsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.DepreciationRead)]
    public Task<PagedResult<DepreciationRunDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, DepreciationRunDto.Projection, Permissions.DepreciationRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.DepreciationRead)]
    public Task<DepreciationRunDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetDepreciationRunQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.DepreciationRun)]
    public Task<DepreciationRunDto> Run(RunDepreciationCommand command, CancellationToken ct)
        => Mediator.Send(command, ct);
}
