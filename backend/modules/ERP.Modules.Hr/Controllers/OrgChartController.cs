using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>The organisation chart: departments and reporting lines.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/org-chart")]
public sealed class OrgChartController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<OrgChartDto> Get(CancellationToken ct) => Mediator.Send(new GetOrgChartQuery(), ct);
}
