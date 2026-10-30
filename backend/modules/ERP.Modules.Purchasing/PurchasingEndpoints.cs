using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Purchasing;

public sealed record ChangePurchaseOrderStatusBody(PurchaseOrderStatus Status);
public sealed record ChangeRequisitionStatusBody(PurchaseRequisitionStatus Status);
public sealed record ConvertRequisitionBody(Guid? VendorPartyId, string VendorName);
public sealed record ConvertToBillBody(Dictionary<Guid, decimal>? UnitPriceOverrides = null);

internal static class PurchasingEndpoints
{
    private const string Base = "/api/v1/m/purchasing";

    public static void Map(RouteGroupBuilder group)
    {
        var orders = group.MapGroup("/orders");
        orders.MapPagedList<PurchaseOrder, PurchaseOrderSummaryDto>("", P.OrdersRead,
            o => new PurchaseOrderSummaryDto(
                o.Id, o.Number, o.LegalEntityId, o.VendorPartyId, o.VendorName, o.VendorReference,
                o.OrderDate, o.ExpectedDeliveryDate, o.Currency, o.Status, o.SubTotal, o.TaxTotal,
                o.DiscountTotal, o.TotalAmount, o.Lines.Count, o.InvoicedBillId, o.CreatedAt));
        orders.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetPurchaseOrder(id), ct)).RequirePermission(P.OrdersRead);
        orders.MapPost("", async (PurchaseOrderInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreatePurchaseOrder(input), ct);
            return Results.Created($"{Base}/orders/{created.Id}", created);
        }).RequirePermission(P.OrdersManage);
        orders.MapPut("/{id:guid}", (Guid id, PurchaseOrderInput input, ISender s, CancellationToken ct) => s.Send(new UpdatePurchaseOrder(id, input), ct)).RequirePermission(P.OrdersManage);
        orders.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeletePurchaseOrder(id), ct); return Results.NoContent(); }).RequirePermission(P.OrdersManage);
        orders.MapPut("/{id:guid}/status", (Guid id, ChangePurchaseOrderStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangePurchaseOrderStatus(id, b.Status), ct)).RequirePermission(P.OrdersManage);
        orders.MapPost("/{id:guid}/convert-to-bill", async (Guid id, HttpRequest req, ISender s, CancellationToken ct) =>
        {
            ConvertToBillBody? body = null;
            if (req.ContentLength > 0)
            {
                body = await req.ReadFromJsonAsync<ConvertToBillBody>(cancellationToken: ct);
            }
            var bill = await s.Send(new ConvertPurchaseOrderToBill(id, body?.UnitPriceOverrides), ct);
            return Results.Ok(bill);
        }).RequirePermission(P.OrdersManage);

        var requisitions = group.MapGroup("/requisitions");
        requisitions.MapPagedList<PurchaseRequisition, RequisitionSummaryDto>("", P.RequisitionsRead,
            r => new RequisitionSummaryDto(
                r.Id, r.Number, r.LegalEntityId, r.RequesterName, r.Department, r.RequisitionDate,
                r.RequiredDate, r.Status, r.EstimatedTotal, r.Lines.Count, r.ConvertedPurchaseOrderId, r.CreatedAt));
        requisitions.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetRequisition(id), ct)).RequirePermission(P.RequisitionsRead);
        requisitions.MapPost("", async (RequisitionInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateRequisition(input), ct);
            return Results.Created($"{Base}/requisitions/{created.Id}", created);
        }).RequirePermission(P.RequisitionsManage);
        requisitions.MapPut("/{id:guid}", (Guid id, RequisitionInput input, ISender s, CancellationToken ct) => s.Send(new UpdateRequisition(id, input), ct)).RequirePermission(P.RequisitionsManage);
        requisitions.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteRequisition(id), ct); return Results.NoContent(); }).RequirePermission(P.RequisitionsManage);
        requisitions.MapPut("/{id:guid}/status", (Guid id, ChangeRequisitionStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeRequisitionStatus(id, b.Status), ct)).RequirePermission(P.RequisitionsManage);
        requisitions.MapPost("/{id:guid}/convert-to-order", (Guid id, ConvertRequisitionBody b, ISender s, CancellationToken ct) => s.Send(new ConvertRequisitionToOrder(id, b.VendorPartyId, b.VendorName), ct)).RequirePermission(P.OrdersManage);

        var receipts = group.MapGroup("/receipts");
        receipts.MapPagedList<GoodsReceipt, GoodsReceiptSummaryDto>("", P.ReceiptsRead,
            g => new GoodsReceiptSummaryDto(
                g.Id, g.Number, g.LegalEntityId, g.PurchaseOrderId, g.PurchaseOrderNumber, g.VendorName,
                g.ReceiptDate, g.ReceivedBy, g.DeliveryNoteNumber, g.Lines.Count, g.CreatedAt));
        receipts.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetGoodsReceipt(id), ct)).RequirePermission(P.ReceiptsRead);
        receipts.MapPost("", async (GoodsReceiptInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateGoodsReceipt(input), ct);
            return Results.Created($"{Base}/receipts/{created.Id}", created);
        }).RequirePermission(P.ReceiptsManage);
        receipts.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteGoodsReceipt(id), ct); return Results.NoContent(); }).RequirePermission(P.ReceiptsManage);

        var kpis = group.MapGroup("/kpis");
        kpis.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetPurchasingKpiSummary(), ct)).RequirePermission(P.OrdersRead);
    }
}
