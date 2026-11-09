using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Customer receipts (cheques received: <see cref="ChequesController"/>).</summary>
[Route("api/v1/finance/receipts")]
public sealed class ReceiptsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.ReceiptsRead)]
    public Task<PagedResult<ReceiptSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => Mediator.Send(new ListReceiptsQuery(query), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.ReceiptsRead)]
    public Task<ReceiptDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetReceiptByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.ReceiptsWrite)]
    public async Task<IActionResult> Create(ReceiptInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateReceiptCommand(input), ct);
        return Created($"/api/v1/finance/receipts/{created.Id}", created);
    }

    [HttpPost("{id:guid}/allocate")]
    [RequirePermission(CorePermissions.ReceiptsWrite)]
    public Task<ReceiptDto> Allocate(Guid id, AllocateRequest body, CancellationToken ct)
        => Mediator.Send(new AllocateReceiptCommand(id, body.Allocations), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(CorePermissions.ReceiptsWrite)]
    public Task<ReceiptDto> Void(Guid id, VoidRequest? body, CancellationToken ct)
        => Mediator.Send(new VoidReceiptCommand(id, body?.Reason), ct);

    /// <summary>The invoices to apply the receipt's unallocated amount to.</summary>
    public sealed record AllocateRequest(List<AllocationInput> Allocations);
}
