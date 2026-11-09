using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Crm.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Crm.Controllers;

/// <summary>CRM analytics and KPIs.</summary>
[ModuleGate(CrmModule.Key)]
[Route("api/v1/m/crm/analytics")]
public sealed class CrmAnalyticsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("kpis")]
    [RequirePermission(Permissions.AnalyticsRead)]
    public Task<CrmKpisDto> Kpis(Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCrmKpisQuery(legalEntityId), ct);
}
