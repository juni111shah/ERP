using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>The lease renewal workbench and bulk renewal drafts.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/renewals")]
public sealed class RenewalsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.LeasesRenew)]
    public Task<RenewalWorkbenchDto> GetWorkbench(int? days, DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetRenewalWorkbenchQuery(days ?? 120, asOf ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow)), ct);

    [HttpPost("drafts")]
    [RequirePermission(P.LeasesRenew)]
    public Task<IReadOnlyList<RenewalDraftResult>> CreateDrafts(RenewalDraftsRequest body, CancellationToken ct)
        => Mediator.Send(new CreateRenewalDraftsCommand(body.LeaseIds, body.UpliftPercent), ct);

    public sealed record RenewalDraftsRequest(List<Guid> LeaseIds, decimal UpliftPercent);
}
