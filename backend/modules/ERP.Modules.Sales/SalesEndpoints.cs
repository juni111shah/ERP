using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Sales;

public sealed record ChangeQuoteStatusBody(SalesQuoteStatus Status);
public sealed record ChangeOrderStatusBody(SalesOrderStatus Status);
public sealed record UpdateFulfillmentBody(List<OrderFulfillmentLineInput> Lines);

internal static class SalesEndpoints
{
    private const string Base = "/api/v1/m/sales";

    public static void Map(RouteGroupBuilder group)
    {
        var quotes = group.MapGroup("/quotes");
        quotes.MapPagedList<SalesQuote, QuoteSummaryDto>("", P.QuotesRead,
            q => new QuoteSummaryDto(
                q.Id, q.Number, q.LegalEntityId, q.CustomerPartyId, q.CustomerName, q.QuoteDate,
                q.ValidUntil, q.Currency, q.Status, q.SubTotal, q.TaxTotal, q.DiscountTotal,
                q.TotalAmount, q.Lines.Count, q.ConvertedSalesOrderId, q.CreatedAt));
        quotes.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetQuote(id), ct)).RequirePermission(P.QuotesRead);
        quotes.MapPost("", async (QuoteInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateQuote(input), ct);
            return Results.Created($"{Base}/quotes/{created.Id}", created);
        }).RequirePermission(P.QuotesManage);
        quotes.MapPut("/{id:guid}", (Guid id, QuoteInput input, ISender s, CancellationToken ct) => s.Send(new UpdateQuote(id, input), ct)).RequirePermission(P.QuotesManage);
        quotes.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteQuote(id), ct); return Results.NoContent(); }).RequirePermission(P.QuotesManage);
        quotes.MapPut("/{id:guid}/status", (Guid id, ChangeQuoteStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeQuoteStatus(id, b.Status), ct)).RequirePermission(P.QuotesManage);
        quotes.MapPost("/{id:guid}/convert-to-order", (Guid id, ISender s, CancellationToken ct) => s.Send(new ConvertQuoteToOrder(id), ct)).RequirePermission(P.OrdersManage);

        var orders = group.MapGroup("/orders");
        orders.MapPagedList<SalesOrder, OrderSummaryDto>("", P.OrdersRead,
            o => new OrderSummaryDto(
                o.Id, o.Number, o.LegalEntityId, o.QuoteId, o.QuoteNumber, o.CustomerPartyId, o.CustomerName,
                o.CustomerReference, o.OrderDate, o.ExpectedDeliveryDate, o.Currency, o.Status,
                o.SubTotal, o.TaxTotal, o.DiscountTotal, o.TotalAmount, o.Lines.Count, o.InvoicedInvoiceId, o.CreatedAt));
        orders.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetOrder(id), ct)).RequirePermission(P.OrdersRead);
        orders.MapPost("", async (OrderInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateOrder(input), ct);
            return Results.Created($"{Base}/orders/{created.Id}", created);
        }).RequirePermission(P.OrdersManage);
        orders.MapPut("/{id:guid}", (Guid id, OrderInput input, ISender s, CancellationToken ct) => s.Send(new UpdateOrder(id, input), ct)).RequirePermission(P.OrdersManage);
        orders.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteOrder(id), ct); return Results.NoContent(); }).RequirePermission(P.OrdersManage);
        orders.MapPut("/{id:guid}/status", (Guid id, ChangeOrderStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeOrderStatus(id, b.Status), ct)).RequirePermission(P.OrdersManage);
        orders.MapPut("/{id:guid}/fulfillment", (Guid id, UpdateFulfillmentBody b, ISender s, CancellationToken ct) => s.Send(new UpdateOrderFulfillment(id, b.Lines), ct)).RequirePermission(P.OrdersManage);
        orders.MapPost("/{id:guid}/convert-to-invoice", (Guid id, ISender s, CancellationToken ct) => s.Send(new ConvertOrderToInvoice(id), ct)).RequirePermission(P.OrdersManage);

        var kpis = group.MapGroup("/kpis");
        kpis.MapGet("", (ISender s, CancellationToken ct) => s.Send(new GetSalesKpiSummary(), ct)).RequirePermission(P.OrdersRead);
    }
}
