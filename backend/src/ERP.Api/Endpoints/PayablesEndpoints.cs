using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modularity;
using MediatR;

namespace ERP.Api.Endpoints;

public sealed record AllocatePaymentBody(List<BillAllocationInput> Allocations);

/// <summary>Vendor bills, vendor payments (including issued cheques) and the payables reports.</summary>
public static class PayablesEndpoints
{
    public static void MapPayables(this RouteGroupBuilder api)
    {
        var bills = api.MapGroup("/finance/bills").WithTags("Payables");
        bills.MapPagedList<Bill, BillSummaryDto>("", CorePermissions.BillsRead,
            b => new BillSummaryDto(b.Id, b.Number, b.PartyId, b.PartyName, b.VendorReference, b.BillDate, b.DueDate, b.Total, b.Paid, b.Status, b.CurrencyCode));
        bills.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetBill(id), ct)).RequirePermission(CorePermissions.BillsRead);
        bills.MapPost("", async (BillInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateBill(input), ct);
            return Results.Created($"/api/v1/finance/bills/{created.Id}", created);
        }).RequirePermission(CorePermissions.BillsWrite);
        bills.MapPut("/{id:guid}", (Guid id, BillInput input, ISender s, CancellationToken ct) => s.Send(new UpdateBill(id, input), ct)).RequirePermission(CorePermissions.BillsWrite);
        bills.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteBill(id), ct); return Results.NoContent(); }).RequirePermission(CorePermissions.BillsWrite);
        bills.MapPost("/{id:guid}/post", (Guid id, ISender s, CancellationToken ct) => s.Send(new PostBill(id), ct)).RequirePermission(CorePermissions.BillsPost);
        bills.MapPost("/{id:guid}/void", (Guid id, ISender s, CancellationToken ct) => s.Send(new VoidBill(id), ct)).RequirePermission(CorePermissions.BillsPost);

        var payments = api.MapGroup("/finance/payments").WithTags("Payables");
        payments.MapGet("", ([AsParameters] PagedQuery q, ISender s, CancellationToken ct) => s.Send(new ListPayments(q), ct)).RequirePermission(CorePermissions.PaymentsRead);
        payments.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPayment(id), ct)).RequirePermission(CorePermissions.PaymentsRead);
        payments.MapPost("", async (PaymentInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreatePayment(input), ct);
            return Results.Created($"/api/v1/finance/payments/{created.Id}", created);
        }).RequirePermission(CorePermissions.PaymentsWrite);
        payments.MapPost("/{id:guid}/allocate", (Guid id, AllocatePaymentBody b, ISender s, CancellationToken ct) => s.Send(new AllocatePayment(id, b.Allocations), ct)).RequirePermission(CorePermissions.PaymentsWrite);
        payments.MapPost("/{id:guid}/void", (Guid id, ISender s, CancellationToken ct) => s.Send(new VoidPayment(id), ct)).RequirePermission(CorePermissions.PaymentsWrite);
        api.MapPost("/finance/cheques/{id:guid}/clear-issued", (Guid id, ClearBody b, ISender s, CancellationToken ct) => s.Send(new ClearIssuedCheque(id, b.Date), ct)).WithTags("Cheques").RequirePermission(CorePermissions.ChequesApprove);

        var reports = api.MapGroup("/finance/reports").WithTags("Finance reports");
        reports.MapGet("/ap-ageing", (DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetApAgeing(asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
        reports.MapGet("/vendor-statement", (Guid partyId, DateOnly asOf, Guid? legalEntityId, ISender s, CancellationToken ct) => s.Send(new GetVendorStatement(partyId, asOf, legalEntityId), ct)).RequirePermission(CorePermissions.ReportsRead);
    }
}
