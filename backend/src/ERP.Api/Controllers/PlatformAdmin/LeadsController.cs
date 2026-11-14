using ERP.Api.Infrastructure;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>Leads from the public assistant (platform console).</summary>
[Route("api/v1/platform-admin/leads")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class LeadsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public Task<PagedResult<BrandLeadRow>> List([FromQuery] ListBrandLeadsQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [PlatformTeamAction("Working the sales leads is day-to-day work.")]
    [HttpPut("{id:guid}/status/{status:regex(^(new|contacted|won|lost)$)}")]
    public Task<BrandLeadRow> SetStatus(Guid id, string status, CancellationToken ct)
        => Mediator.Send(new SetBrandLeadStatusCommand(id, Enum.Parse<BrandLeadStatus>(status, ignoreCase: true)), ct);

    [PlatformTeamAction("Removing a spam or duplicate lead is day-to-day work.")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBrandLeadCommand(id), ct);
        return NoContent();
    }
}
