using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Platform;

/// <summary>A client's own plan and invoices.</summary>
[Route("api/v1/billing")]
public sealed class BillingController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("subscription")]
    [RequirePermission(CorePermissions.BillingRead)]
    public Task<BillingOverview> GetSubscription(CancellationToken ct) => Mediator.Send(new GetBillingQuery(), ct);

    [HttpGet("invoices/{id:guid}")]
    [RequirePermission(CorePermissions.BillingRead)]
    public Task<InvoiceDocument> GetInvoice(Guid id, CancellationToken ct) => Mediator.Send(new GetMyInvoiceQuery(id), ct);

    [HttpPost("change-request")]
    [RequirePermission(CorePermissions.BillingManage)]
    public Task<BillingOverview> RequestPlanChange(PlanChangeInput input, CancellationToken ct) => Mediator.Send(new RequestPlanChangeCommand(input), ct);

    [HttpDelete("change-request")]
    [RequirePermission(CorePermissions.BillingManage)]
    public Task<BillingOverview> WithdrawPlanChange(CancellationToken ct) => Mediator.Send(new WithdrawPlanChangeCommand(), ct);
}
