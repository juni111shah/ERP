using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Pos;

public sealed record VoidOrderBody(string Reason);

internal static class PosEndpoints
{
    private const string Base = "/api/v1/m/pos";

    public static void Map(RouteGroupBuilder group)
    {
        // Registers
        var registers = group.MapGroup("/registers");
        registers.MapPagedList<PosRegister, PosRegisterDto>("", Permissions.RegistersRead,
            r => new PosRegisterDto(
                r.Id, r.LegalEntityId, r.Code, r.Name, r.WarehouseId, r.DefaultCashAccountId,
                r.IsActive, r.ReceiptHeader, r.ReceiptFooter, r.CustomFields, r.CreatedAt, r.CardAccountId, r.VarianceAccountId, r.ExpenseAccountId));
        registers.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var r = await s.Send(new GetRegisterByIdQuery(id), ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).RequirePermission(Permissions.RegistersRead);
        registers.MapPost("", async (CreateRegisterCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/registers/{created.Id}", created);
        }).RequirePermission(Permissions.RegistersManage);
        registers.MapPut("/{id:guid}", (Guid id, UpdateRegisterCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.RegistersManage);
        registers.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteRegisterCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.RegistersManage);

        // Sessions (Shifts)
        var sessions = group.MapGroup("/sessions");
        sessions.MapPagedList<PosSession, PosSessionSummaryDto>("", Permissions.SessionsRead,
            s => new PosSessionSummaryDto(
                s.Id, s.LegalEntityId, s.RegisterId, s.RegisterName, s.OpenedByUserId, s.CashierName,
                s.OpenedAt, s.ClosedByUserId, s.ClosedAt, s.OpeningCash, s.TotalSales, s.TotalReturns,
                s.ExpectedCash, s.ActualCash, s.CashDifference, s.Status, s.Notes, s.CreatedAt, s.TotalPayIn, s.TotalPayOut));
        sessions.MapGet("/current", (Guid registerId, ISender s, CancellationToken ct) =>
            s.Send(new GetCurrentSessionQuery(registerId), ct)).RequirePermission(Permissions.SessionsRead);
        sessions.MapGet("/{id:guid}/report", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new GetPosShiftReportQuery(id), ct)).RequirePermission(Permissions.SessionsRead);
        sessions.MapPost("/open", async (OpenSessionCommand cmd, ISender s, CancellationToken ct) =>
        {
            var opened = await s.Send(cmd, ct);
            return Results.Created($"{Base}/sessions/{opened.Id}", opened);
        }).RequirePermission(Permissions.SessionsManage);
        sessions.MapPost("/{id:guid}/close", (Guid id, CloseSessionCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { SessionId = id }, ct)).RequirePermission(Permissions.SessionsManage);
        sessions.MapPost("/{id:guid}/drawer-transaction", (Guid id, RecordDrawerTransactionCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { SessionId = id }, ct)).RequirePermission(Permissions.SessionsManage);

        // Counter Sales Orders
        var orders = group.MapGroup("/orders");
        orders.MapPagedList<PosOrder, PosOrderSummaryDto>("", Permissions.OrdersRead,
            o => new PosOrderSummaryDto(
                o.Id, o.Number, o.LegalEntityId, o.RegisterId, o.SessionId, o.CustomerPartyId,
                o.CustomerName, o.OrderDate, o.SubTotal, o.TaxTotal, o.DiscountTotal,
                o.TotalAmount, o.Status, o.PaymentStatus, o.CashierName, o.Lines.Count, o.CreatedAt, o.ReturnedTotal, o.InvoiceNumber));
        orders.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new GetPosOrderByIdQuery(id), ct)).RequirePermission(Permissions.OrdersRead);
        orders.MapGet("/by-number/{number}", (string number, ISender s, CancellationToken ct) =>
            s.Send(new GetPosOrderByNumberQuery(number), ct)).RequirePermission(Permissions.OrdersRead);
        orders.MapPost("", async (CreatePosOrderCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/orders/{created.Id}", created);
        }).RequirePermission(Permissions.OrdersCreate);
        orders.MapPost("/{id:guid}/void", (Guid id, VoidOrderBody b, ISender s, CancellationToken ct) =>
            s.Send(new VoidPosOrderCommand(id, b.Reason), ct)).RequirePermission(Permissions.OrdersVoid);
        orders.MapPost("/park", async (ParkPosOrderCommand cmd, ISender s, CancellationToken ct) =>
        {
            var parked = await s.Send(cmd, ct);
            return Results.Created($"{Base}/orders/{parked.Id}", parked);
        }).RequirePermission(Permissions.OrdersCreate);
        orders.MapDelete("/{id:guid}/park", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DiscardParkedOrderCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.OrdersCreate);

        // Returns
        var returns = group.MapGroup("/returns");
        returns.MapPagedList<PosReturn, PosReturnSummaryDto>("", Permissions.OrdersRead,
            r => new PosReturnSummaryDto(
                r.Id, r.Number, r.LegalEntityId, r.OriginalOrderId, r.OriginalNumber, r.RegisterId, r.SessionId, r.CustomerName,
                r.ReturnDate, r.SubTotal, r.TaxTotal, r.TotalAmount, r.Reason, r.CashierName, r.CreditNoteNumber));
        returns.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new GetPosReturnByIdQuery(id), ct)).RequirePermission(Permissions.OrdersRead);
        returns.MapPost("", async (CreatePosReturnCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/returns/{created.Id}", created);
        }).RequirePermission(Permissions.OrdersRefund);

        // The till: everything here asks only for the right to sell.
        var terminal = group.MapGroup("/terminal");
        terminal.MapGet("", (ISender s, CancellationToken ct) =>
            s.Send(new GetPosTerminalQuery(), ct)).RequirePermission(Permissions.OrdersCreate);
        terminal.MapGet("/session", (Guid registerId, ISender s, CancellationToken ct) =>
            s.Send(new GetCurrentSessionQuery(registerId), ct)).RequirePermission(Permissions.OrdersCreate);
        terminal.MapGet("/catalog", (Guid? registerId, string? search, string? code, int? page, int? pageSize, ISender s, CancellationToken ct) =>
            s.Send(new GetPosCatalogQuery(registerId, search, code, page ?? 1, pageSize ?? 60), ct)).RequirePermission(Permissions.OrdersCreate);
        terminal.MapGet("/customers", (string? search, ISender s, CancellationToken ct) =>
            s.Send(new GetPosCustomersQuery(search), ct)).RequirePermission(Permissions.OrdersCreate);
        terminal.MapGet("/held", (Guid? registerId, ISender s, CancellationToken ct) =>
            s.Send(new GetHeldOrdersQuery(registerId), ct)).RequirePermission(Permissions.OrdersCreate);

        // Analytics / KPIs
        group.MapGet("/kpis", (Guid? legalEntityId, ISender s, CancellationToken ct) =>
            s.Send(new GetPosKpiSummaryQuery(legalEntityId), ct)).RequirePermission(Permissions.AnalyticsRead);
        group.MapGet("/reports/sales", (DateOnly? from, DateOnly? to, Guid? registerId, Guid? legalEntityId, ISender s, CancellationToken ct) =>
            s.Send(new GetPosSalesReportQuery(from, to, registerId, legalEntityId), ct)).RequirePermission(Permissions.AnalyticsRead);
    }
}
