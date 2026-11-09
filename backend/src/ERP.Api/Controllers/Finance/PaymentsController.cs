using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Vendor payments, including issued cheques (clearing one: <see cref="IssuedChequesController"/>).</summary>
[Route("api/v1/finance/payments")]
public sealed class PaymentsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.PaymentsRead)]
    public Task<PagedResult<PaymentSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => Mediator.Send(new ListPaymentsQuery(query), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.PaymentsRead)]
    public Task<PaymentDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPaymentByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.PaymentsWrite)]
    public async Task<IActionResult> Create(PaymentInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePaymentCommand(input), ct);
        return Created($"/api/v1/finance/payments/{created.Id}", created);
    }

    [HttpPost("{id:guid}/allocate")]
    [RequirePermission(CorePermissions.PaymentsWrite)]
    public Task<PaymentDto> Allocate(Guid id, AllocatePaymentRequest body, CancellationToken ct)
        => Mediator.Send(new AllocatePaymentCommand(id, body.Allocations), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(CorePermissions.PaymentsWrite)]
    public Task<PaymentDto> Void(Guid id, VoidRequest? body, CancellationToken ct)
        => Mediator.Send(new VoidPaymentCommand(id, body?.Reason), ct);

    [HttpPost("{id:guid}/post")]
    [RequirePermission(CorePermissions.PaymentsWrite)]
    public Task<PaymentDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostPaymentCommand(id), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.PaymentsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePaymentCommand(id), ct);
        return NoContent();
    }

    /// <summary>The bills to apply the payment's unallocated amount to.</summary>
    public sealed record AllocatePaymentRequest(List<BillAllocationInput> Allocations);
}
