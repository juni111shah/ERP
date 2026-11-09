using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Project billing: what is ready to bill, billing it, writing it off, and moving cost into the ledger.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/billing")]
public sealed class BillingController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("overview")]
    [RequirePermission(P.BillingRead)]
    public Task<IReadOnlyList<ProjectBillingSummaryDto>> Overview(CancellationToken ct)
        => Mediator.Send(new GetBillingOverviewQuery(), ct);

    [HttpGet("{projectId:guid}/billable")]
    [RequirePermission(P.BillingRead)]
    public Task<BillableWorkDto> Billable(Guid projectId, CancellationToken ct)
        => Mediator.Send(new GetBillableWorkQuery(projectId), ct);

    [HttpGet("{projectId:guid}/invoices")]
    [RequirePermission(P.BillingRead)]
    public Task<IReadOnlyList<ProjectInvoiceDto>> Invoices(Guid projectId, CancellationToken ct)
        => Mediator.Send(new GetProjectInvoicesQuery(projectId), ct);

    [HttpPost("write-off")]
    [RequirePermission(P.BillingManage)]
    public async Task<IActionResult> WriteOff(ProjectRequest body, CancellationToken ct)
        => Ok(new { writtenOff = await Mediator.Send(new WriteOffProjectCommand(body.ProjectId), ct) });

    [HttpPost("bill")]
    [RequirePermission(P.BillingManage)]
    public Task<BillProjectResultDto> Bill(BillProjectInput input, CancellationToken ct)
        => Mediator.Send(new BillProjectCommand(input), ct);

    [HttpPost("post-costs")]
    [RequirePermission(P.BillingManage)]
    public Task<PostCostsResult> PostCosts(ProjectRequest body, CancellationToken ct)
        => Mediator.Send(new PostProjectCostsCommand(body.ProjectId), ct);

    [HttpPost("preview")]
    [RequirePermission(P.BillingRead)]
    public Task<BillPreviewDto> Preview(BillProjectInput input, CancellationToken ct)
        => Mediator.Send(new PreviewProjectBillQuery(input), ct);

    /// <summary>The project a write-off or a cost posting applies to.</summary>
    public sealed record ProjectRequest(Guid ProjectId);
}
