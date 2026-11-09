using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>HR reports: the summary, birthdays and anniversaries, end-of-service liability and its provision.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/reports")]
public sealed class HrReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("summary")]
    [RequirePermission(P.EmployeesRead)]
    public Task<HrSummaryReportDto> Summary(DateOnly? from, DateOnly? to, Guid? legalEntityId, CancellationToken ct) => Mediator.Send(new GetHrSummaryReportQuery(from, to, legalEntityId), ct);

    [HttpGet("birthdays-anniversaries")]
    [RequirePermission(P.EmployeesRead)]
    public Task<HrOccasionsDto> BirthdaysAnniversaries(int? days, Guid? legalEntityId, CancellationToken ct) => Mediator.Send(new GetBirthdaysAnniversariesQuery(days ?? 30, legalEntityId), ct);

    [HttpGet("gratuity")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<GratuityReportDto> Gratuity(DateOnly asOf, Guid? legalEntityId, CancellationToken ct) => Mediator.Send(new GetGratuityReportQuery(asOf, legalEntityId), ct);

    [HttpPost("gratuity/provision")]
    [RequirePermission(P.PayrollPost)]
    public Task<GratuityProvisionDto> PostGratuityProvision(ProvisionRequest b, CancellationToken ct) => Mediator.Send(new PostGratuityProvisionCommand(b.LegalEntityId, b.AsOf), ct);

    /// <summary>The legal entity and the date the provision is trued up to.</summary>
    public sealed record ProvisionRequest(Guid LegalEntityId, DateOnly AsOf);
}
