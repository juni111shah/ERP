using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record ApplyCreditBody(Guid InvoiceId, decimal Amount);
public sealed record AllocateBody(List<AllocationInput> Allocations);

/// <summary>Invoices, credit notes, customer receipts and the receivables reports.</summary>
public static class ReceivablesEndpoints
{
    public static void MapReceivables(this RouteGroupBuilder api)
    {
        var invoices = api.MapGroup("/finance/invoices").WithTags("Receivables");
        invoices.MapPagedList<Invoice, InvoiceSummaryDto>("", CorePermissions.InvoicesRead,
            i => new InvoiceSummaryDto(i.Id, i.Number, i.IsCreditNote, i.PartyId, i.PartyName, i.InvoiceDate, i.DueDate, i.Total, i.Paid, i.Status, i.CurrencyCode));
        invoices.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetInvoice(id), ct)).RequirePermission(CorePermissions.InvoicesRead);
        invoices.MapPost("", async (InvoiceInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateInvoice(input), ct);
            return Results.Created($"/api/v1/finance/invoices/{created.Id}", created);
        }).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapPut("/{id:guid}", (Guid id, InvoiceInput input, ISender s, CancellationToken ct) => s.Send(new UpdateInvoice(id, input), ct)).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteInvoice(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapPost("/{id:guid}/issue", (Guid id, ISender s, CancellationToken ct) => s.Send(new IssueInvoice(id), ct)).RequirePermission(CorePermissions.InvoicesIssue);
        invoices.MapPost("/{id:guid}/void", (Guid id, ISender s, CancellationToken ct) => s.Send(new VoidInvoice(id), ct)).RequirePermission(CorePermissions.InvoicesIssue);
        invoices.MapPost("/{id:guid}/apply-credit", (Guid id, ApplyCreditBody body, ISender s, CancellationToken ct) => s.Send(new ApplyCreditNote(id, body.InvoiceId, body.Amount), ct)).RequirePermission(CorePermissions.InvoicesIssue);

        var receipts = api.MapGroup("/finance/receipts").WithTags("Receivables");
        receipts.MapPagedList<Settlement, ReceiptSummaryDto>("", CorePermissions.ReceiptsRead,
            r => new ReceiptSummaryDto(r.Id, r.Number, r.PartyId, r.PartyName, r.Date, r.Method, r.Amount, r.Unallocated, r.Status, r.CurrencyCode));
        receipts.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetReceipt(id), ct)).RequirePermission(CorePermissions.ReceiptsRead);
        receipts.MapPost("", async (ReceiptInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateReceipt(input), ct);
            return Results.Created($"/api/v1/finance/receipts/{created.Id}", created);
        }).RequirePermission(CorePermissions.ReceiptsWrite);
        receipts.MapPost("/{id:guid}/allocate", (Guid id, AllocateBody body, ISender s, CancellationToken ct) => s.Send(new AllocateReceipt(id, body.Allocations), ct)).RequirePermission(CorePermissions.ReceiptsWrite);
        receipts.MapPost("/{id:guid}/void", (Guid id, ISender s, CancellationToken ct) => s.Send(new VoidReceipt(id), ct)).RequirePermission(CorePermissions.ReceiptsWrite);

        var reports = api.MapGroup("/finance/reports").WithTags("Finance reports");
        reports.MapGet("/ar-ageing", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetArAgeing(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/customer-statement", (Guid partyId, DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCustomerStatement(partyId, asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
    }
}
