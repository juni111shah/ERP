using ERP.Application.Dashboard;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Dashboard;

/// <summary>
/// The overview: cards, charts and alerts from core and every enabled module, limited to what the caller may see.
/// Optional <c>from</c>/<c>to</c> (yyyy-MM-dd) set the reporting period; the default is the last year ending today.
/// </summary>
[Route("api/v1/dashboard")]
public sealed class DashboardController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [SignedIn("Each card is computed only for holders of its read permission")]
    public Task<DashboardDto> Get(Guid? legalEntityId, DateOnly? from, DateOnly? to, CancellationToken ct)
        => Mediator.Send(new GetDashboardQuery(legalEntityId, from, to), ct);

    [HttpGet("owner")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<OwnerDashboardDto> GetOwner(Guid? legalEntityId, DateOnly? from, DateOnly? to, CancellationToken ct)
        => Mediator.Send(new GetOwnerDashboardQuery(legalEntityId, from, to), ct);
}
