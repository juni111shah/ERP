using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>POS KPIs and the sales report.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos")]
[RequirePermission(Permissions.AnalyticsRead)]
public sealed class PosAnalyticsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("kpis")]
    public Task<PosKpiSummaryDto> GetKpis(Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetPosKpiSummaryQuery(legalEntityId), ct);

    [HttpGet("reports/sales")]
    public Task<PosSalesReportDto> GetSalesReport(DateOnly? from, DateOnly? to, Guid? registerId, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetPosSalesReportQuery(from, to, registerId, legalEntityId), ct);
}
