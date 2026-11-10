using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>Customer invoices and credit notes.</summary>
[Route("api/v1/finance/invoices")]
public sealed class InvoicesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.InvoicesRead)]
    public Task<PagedResult<InvoiceSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, InvoiceSummaryDto.Projection, CorePermissions.InvoicesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.InvoicesRead)]
    public Task<InvoiceDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetInvoiceByIdQuery(id), ct);

    [HttpGet("{id:guid}/allocations")]
    [RequirePermission(CorePermissions.InvoicesRead)]
    public Task<IReadOnlyList<InvoiceAllocationDto>> GetAllocations(Guid id, CancellationToken ct)
        => Mediator.Send(new GetInvoiceAllocationsQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.InvoicesWrite)]
    public async Task<IActionResult> Create(InvoiceInput input, CancellationToken ct)
    {
        var invoice = await Mediator.Send(new CreateInvoiceCommand(input), ct);
        return Created($"/api/v1/finance/invoices/{invoice.Id}", invoice);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.InvoicesWrite)]
    public Task<InvoiceDto> Update(Guid id, InvoiceInput input, CancellationToken ct)
        => Mediator.Send(new UpdateInvoiceCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.InvoicesWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteInvoiceCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/issue")]
    [RequirePermission(CorePermissions.InvoicesIssue)]
    public Task<InvoiceDto> Issue(Guid id, CancellationToken ct)
        => Mediator.Send(new IssueInvoiceCommand(id), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(CorePermissions.InvoicesVoid)]
    public Task<InvoiceDto> Void(Guid id, VoidRequest? body, CancellationToken ct)
        => Mediator.Send(new VoidInvoiceCommand(id, body?.Reason), ct);

    [HttpPost("{id:guid}/apply-credit")]
    [RequirePermission(CorePermissions.InvoicesCredit)]
    public Task<InvoiceDto> ApplyCredit(Guid id, ApplyCreditRequest body, CancellationToken ct)
        => Mediator.Send(new ApplyCreditNoteCommand(id, body.InvoiceId, body.Amount), ct);

    [HttpPost("{id:guid}/credit-note")]
    [RequirePermission(CorePermissions.InvoicesCredit)]
    public async Task<IActionResult> CreateCreditNote(Guid id, CreditNoteFromInvoiceRequest? body, CancellationToken ct)
    {
        var creditNote = await Mediator.Send(new CreateCreditNoteFromInvoiceCommand(id, body?.Date, body?.Lines, body?.Notes), ct);
        return Created($"/api/v1/finance/invoices/{creditNote.Id}", creditNote);
    }

    /// <summary>Applies the credit note <c>{id}</c> to <see cref="InvoiceId"/>.</summary>
    public sealed record ApplyCreditRequest(Guid InvoiceId, decimal Amount);

    /// <summary>Optional: the credit-note date, the lines to credit (all uncredited when empty) and notes.</summary>
    public sealed record CreditNoteFromInvoiceRequest(DateOnly? Date, List<CreditNoteLineInput>? Lines, string? Notes);
}
