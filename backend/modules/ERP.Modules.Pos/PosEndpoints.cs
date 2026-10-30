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
                r.IsActive, r.ReceiptHeader, r.ReceiptFooter, r.CustomFields, r.CreatedAt));
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
                s.ExpectedCash, s.ActualCash, s.CashDifference, s.Status, s.Notes, s.CreatedAt));
        sessions.MapGet("/current", (Guid registerId, ISender s, CancellationToken ct) =>
            s.Send(new GetCurrentSessionQuery(registerId), ct)).RequirePermission(Permissions.SessionsRead);
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
                o.TotalAmount, o.Status, o.PaymentStatus, o.CashierName, o.Lines.Count, o.CreatedAt));
        orders.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new GetPosOrderByIdQuery(id), ct)).RequirePermission(Permissions.OrdersRead);
        orders.MapPost("", async (CreatePosOrderCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/orders/{created.Id}", created);
        }).RequirePermission(Permissions.OrdersCreate);
        orders.MapPost("/{id:guid}/void", (Guid id, VoidOrderBody b, ISender s, CancellationToken ct) =>
            s.Send(new VoidPosOrderCommand(id, b.Reason), ct)).RequirePermission(Permissions.OrdersVoid);

        // Analytics / KPIs
        group.MapGet("/kpis", (Guid? legalEntityId, ISender s, CancellationToken ct) =>
            s.Send(new GetPosKpiSummaryQuery(legalEntityId), ct)).RequirePermission(Permissions.AnalyticsRead);
    }
}
