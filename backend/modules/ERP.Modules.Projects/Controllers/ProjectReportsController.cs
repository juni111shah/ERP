using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Project reports: profitability, utilisation, budget, unbilled ageing and hours.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/reports")]
public sealed class ProjectReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("profitability")]
    [RequirePermission(P.ReportsRead)]
    public Task<IReadOnlyList<ProfitabilityRow>> Profitability(DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct)
        => Mediator.Send(new GetProfitabilityReportQuery(from, to, projectId), ct);

    [HttpGet("utilisation")]
    [RequirePermission(P.ReportsRead)]
    public Task<IReadOnlyList<UtilisationRow>> Utilisation(DateOnly from, DateOnly to, CancellationToken ct)
        => Mediator.Send(new GetUtilisationReportQuery(from, to), ct);

    [HttpGet("budget")]
    [RequirePermission(P.ReportsRead)]
    public Task<IReadOnlyList<BudgetRow>> Budget(CancellationToken ct)
        => Mediator.Send(new GetBudgetReportQuery(), ct);

    [HttpGet("unbilled-ageing")]
    [RequirePermission(P.ReportsRead)]
    public Task<IReadOnlyList<AgeingRow>> UnbilledAgeing(DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetUnbilledAgeingQuery(asOf), ct);

    [HttpGet("hours")]
    [RequirePermission(P.ReportsRead)]
    public Task<IReadOnlyList<HoursRow>> Hours(DateOnly from, DateOnly to, string? groupBy, Guid? projectId, CancellationToken ct)
        => Mediator.Send(new GetHoursReportQuery(from, to, groupBy ?? "person", projectId), ct);
}
