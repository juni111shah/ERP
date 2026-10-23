using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record ApplyCreditBody(Guid InvoiceId, decimal Amount);
public sealed record AllocateBody(List<AllocationInput> Allocations);
/// <summary>Body of the invoice, receipt, bill and payment void endpoints: { "reason": string }, required, 3-500 characters after trimming.
/// A missing body or reason is rejected by the command validator with 400 (errors.reason), like a short or long one.</summary>
public sealed record VoidBody(string? Reason);
public sealed record CreditNoteFromInvoiceBody(DateOnly? Date, List<CreditNoteLineInput>? Lines, string? Notes);

/// <summary>Invoices, credit notes, customer receipts and the receivables reports.</summary>
public static class ReceivablesEndpoints
{
    public static void MapReceivables(this RouteGroupBuilder api)
    {
        var invoices = api.MapGroup("/finance/invoices").WithTags("Receivables");
        invoices.MapPagedList<Invoice, InvoiceSummaryDto>("", CorePermissions.InvoicesRead,
            i => new InvoiceSummaryDto(i.Id, i.Number, i.IsCreditNote, i.PartyId, i.PartyName, i.InvoiceDate, i.DueDate, i.Total, i.Paid, i.Status, i.CurrencyCode, i.Net, i.Tax, i.SourceType));
        invoices.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetInvoice(id), ct)).RequirePermission(CorePermissions.InvoicesRead);
        invoices.MapPost("", async (InvoiceInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateInvoice(input), ct);
            return Results.Created($"/api/v1/finance/invoices/{created.Id}", created);
        }).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapPut("/{id:guid}", (Guid id, InvoiceInput input, ISender s, CancellationToken ct) => s.Send(new UpdateInvoice(id, input), ct)).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteInvoice(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.InvoicesWrite);
        invoices.MapGet("/{id:guid}/allocations", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetInvoiceAllocations(id), ct)).RequirePermission(CorePermissions.InvoicesRead);
        invoices.MapPost("/{id:guid}/issue", (Guid id, ISender s, CancellationToken ct) => s.Send(new IssueInvoice(id), ct)).RequirePermission(CorePermissions.InvoicesIssue);
        invoices.MapPost("/{id:guid}/void", (Guid id, VoidBody? body, ISender s, CancellationToken ct) => s.Send(new VoidInvoice(id, body?.Reason), ct)).RequirePermission(CorePermissions.InvoicesVoid);
        invoices.MapPost("/{id:guid}/apply-credit", (Guid id, ApplyCreditBody body, ISender s, CancellationToken ct) => s.Send(new ApplyCreditNote(id, body.InvoiceId, body.Amount), ct)).RequirePermission(CorePermissions.InvoicesCredit);

        invoices.MapPost("/{id:guid}/credit-note", async (Guid id, CreditNoteFromInvoiceBody? body, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateCreditNoteFromInvoice(id, body?.Date, body?.Lines, body?.Notes), ct);
            return Results.Created($"/api/v1/finance/invoices/{created.Id}", created);
        }).RequirePermission(CorePermissions.InvoicesCredit);

        var receipts = api.MapGroup("/finance/receipts").WithTags("Receivables");
        receipts.MapGet("", ([AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListReceipts(q), ct)).RequirePermission(CorePermissions.ReceiptsRead);
        receipts.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetReceipt(id), ct)).RequirePermission(CorePermissions.ReceiptsRead);
        receipts.MapPost("", async (ReceiptInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateReceipt(input), ct);
            return Results.Created($"/api/v1/finance/receipts/{created.Id}", created);
        }).RequirePermission(CorePermissions.ReceiptsWrite);
        receipts.MapPost("/{id:guid}/allocate", (Guid id, AllocateBody body, ISender s, CancellationToken ct) => s.Send(new AllocateReceipt(id, body.Allocations), ct)).RequirePermission(CorePermissions.ReceiptsWrite);
        receipts.MapPost("/{id:guid}/void", (Guid id, VoidBody? body, ISender s, CancellationToken ct) => s.Send(new VoidReceipt(id, body?.Reason), ct)).RequirePermission(CorePermissions.ReceiptsWrite);


        var reports = api.MapGroup("/finance/reports").WithTags("Finance reports");
        reports.MapGet("/ar-ageing", (DateOnly asOf, Guid? legalEntityId, Guid? businessUnitId, string? sourceType, string? groupBy, ISender s, CancellationToken ct) => s.Send(new GetArAgeing(asOf, legalEntityId, businessUnitId, sourceType, groupBy), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/customer-statement", (Guid partyId, DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCustomerStatement(partyId, asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/customer-ledger", (Guid partyId, DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCustomerLedger(partyId, from, to, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
    }
}
