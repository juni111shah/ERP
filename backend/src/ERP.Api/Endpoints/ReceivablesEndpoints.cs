using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record AllocateBody(List<AllocationInput> Allocations);
/// <summary>Body of the invoice, receipt, bill and payment void endpoints: { "reason": string }, required, 3-500 characters after trimming.
/// A missing body or reason is rejected by the command validator with 400 (errors.reason), like a short or long one.</summary>
public sealed record VoidBody(string? Reason);

/// <summary>Customer receipts and the receivables reports (invoices: Controllers/Finance/InvoicesController).</summary>
public static class ReceivablesEndpoints
{
    public static void MapReceivables(this RouteGroupBuilder api)
    {
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
        reports.MapGet("/ar-ageing", (DateOnly asOf, Guid? legalEntityId, Guid? businessUnitId, string? sourceType, string? groupBy, Guid? partyGroupId, ISender s, CancellationToken ct) => s.Send(new GetArAgeing(asOf, legalEntityId, businessUnitId, sourceType, groupBy, partyGroupId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/customer-statement", (Guid partyId, DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCustomerStatement(partyId, asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/customer-ledger", (Guid partyId, DateOnly from, DateOnly to, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetCustomerLedger(partyId, from, to, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
    }
}
