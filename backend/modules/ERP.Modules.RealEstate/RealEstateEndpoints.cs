using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate;

public sealed record UnitStatusBody(UnitAction Action, DateOnly? Until = null, string? Reason = null);
public sealed record TerminateBody(DateOnly Date, string Reason);
public sealed record HoldingOverBody(bool HoldingOver);
public sealed record WorkOrderStatusBody(WorkOrderStatus Status, decimal? ActualCost = null, string? ResolutionNotes = null);
public sealed record DealStageBody(DealStage Stage, string? LostReason = null);

/// <summary>Thin controllers: every endpoint sends one MediatR request (see docs/ARCHITECTURE.md, request flow).</summary>
internal static class RealEstateEndpoints
{
    private const string Base = "/api/v1/m/realestate";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/status", () => Results.Ok(new { module = RealEstateModule.Key, message = "Real estate module is installed and enabled." })).RequirePermission(P.PropertiesRead);
        MapUnitTypes(group.MapGroup("/unit-types"));
        MapProperties(group.MapGroup("/properties"));
        MapUnits(group.MapGroup("/units"));
        MapLeases(group.MapGroup("/leases"));
        MapWorkOrders(group.MapGroup("/work-orders"));
        MapDeals(group.MapGroup("/deals"));
    }

    private static void MapUnitTypes(RouteGroupBuilder g)
    {
        g.MapPagedList<UnitType, UnitTypeDto>("", P.UnitTypesRead, t => new UnitTypeDto(t.Id, t.Code, t.Name, t.DefaultUsage, t.DefaultBedrooms, t.IsActive));
        g.MapPost("", async (UnitTypeInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateUnitType(input), ct);
            return Results.Created($"{Base}/unit-types/{created.Id}", created);
        }).RequirePermission(P.UnitTypesManage);
        g.MapPut("/{id:guid}", (Guid id, UnitTypeInput input, ISender s, CancellationToken ct) => s.Send(new UpdateUnitType(id, input), ct)).RequirePermission(P.UnitTypesManage);
    }

    private static void MapProperties(RouteGroupBuilder g)
    {
        g.MapPagedList<Property, PropertyDto>("", P.PropertiesRead, Docs.Property, PropertyDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetProperty(id), ct)).RequirePermission(P.PropertiesRead);
        g.MapPost("", async (PropertyInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateProperty(input), ct);
            return Results.Created($"{Base}/properties/{created.Id}", created);
        }).RequirePermission(P.PropertiesManage);
        g.MapPut("/{id:guid}", (Guid id, PropertyInput input, ISender s, CancellationToken ct) => s.Send(new UpdateProperty(id, input), ct)).RequirePermission(P.PropertiesManage);
    }

    private static void MapUnits(RouteGroupBuilder g)
    {
        g.MapGet("", ([AsParameters] ListUnits query, ISender s, CancellationToken ct) => s.Send(query, ct)).RequirePermission(P.UnitsRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetUnit(id), ct)).RequirePermission(P.UnitsRead);
        g.MapGet("/{id:guid}/rent-history", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetUnitRentHistory(id), ct)).RequirePermission(P.UnitsRead);
        g.MapPost("", async (UnitInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateUnit(input), ct);
            return Results.Created($"{Base}/units/{created.Id}", created);
        }).RequirePermission(P.UnitsManage);
        g.MapPost("/bulk", async (BulkUnitsInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/units", await s.Send(new BulkCreateUnits(input), ct)))
            .RequirePermission(P.UnitsManage);
        g.MapPut("/{id:guid}", (Guid id, UnitInput input, ISender s, CancellationToken ct) => s.Send(new UpdateUnit(id, input), ct)).RequirePermission(P.UnitsManage);
        // Reserve/release need units.reserve, the rest units.manage: the command declares the permission for its action.
        g.MapPost("/{id:guid}/status", (Guid id, UnitStatusBody body, ISender s, CancellationToken ct) => s.Send(new ChangeUnitStatus(id, body.Action, body.Until, body.Reason), ct))
            .RequireAuthorization();
    }

    private static void MapLeases(RouteGroupBuilder g)
    {
        g.MapGet("", ([AsParameters] ListLeases query, ISender s, CancellationToken ct) => s.Send(query, ct)).RequirePermission(P.LeasesRead);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetLease(id), ct)).RequirePermission(P.LeasesRead);
        g.MapGet("/{id:guid}/schedule", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetLease(id), ct)).Schedule).RequirePermission(P.LeasesRead);
        g.MapPost("", async (LeaseInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateLease(input), ct);
            return Results.Created($"{Base}/leases/{created.Id}", created);
        }).RequireAuthorization(); // leases.create, or leases.activate with ActivateNow: the command declares which
        g.MapPut("/{id:guid}", (Guid id, LeaseInput input, ISender s, CancellationToken ct) => s.Send(new UpdateLease(id, input), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/activate", (Guid id, ISender s, CancellationToken ct) => s.Send(new ActivateLease(id), ct)).RequirePermission(P.LeasesActivate);
        g.MapPost("/{id:guid}/renew", async (Guid id, RenewalInput? input, ISender s, CancellationToken ct) =>
        {
            var renewal = await s.Send(new RenewLease(id, input ?? new(null, null, null, null, null)), ct);
            return Results.Created($"{Base}/leases/{renewal.Id}", renewal);
        }).RequirePermission(P.LeasesRenew);
        g.MapPost("/{id:guid}/terminate", (Guid id, TerminateBody body, ISender s, CancellationToken ct) => s.Send(new TerminateLease(id, body.Date, body.Reason), ct)).RequirePermission(P.LeasesTerminate);
        g.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelLease(id), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/holding-over", (Guid id, HoldingOverBody body, ISender s, CancellationToken ct) => s.Send(new FlagHoldingOver(id, body.HoldingOver), ct)).RequirePermission(P.LeasesEdit);
        g.MapPost("/{id:guid}/move-out", (Guid id, ISender s, CancellationToken ct) => s.Send(new ConfirmLeaseMoveOut(id), ct)).RequirePermission(P.LeasesTerminate);
        g.MapPost("/process-expiries", (ISender s, CancellationToken ct) => s.Send(new ProcessLeaseExpiries(), ct)).RequirePermission(P.LeasesActivate);
    }

    private static void MapWorkOrders(RouteGroupBuilder g)
    {
        g.MapPagedList<WorkOrder, WorkOrderDto>("", P.WorkOrdersRead, Docs.WorkOrder, WorkOrderDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetWorkOrder(id), ct)).RequirePermission(P.WorkOrdersRead);
        g.MapPost("", async (WorkOrderInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateWorkOrder(input), ct);
            return Results.Created($"{Base}/work-orders/{created.Id}", created);
        }).RequirePermission(P.WorkOrdersCreate);
        g.MapPut("/{id:guid}", (Guid id, WorkOrderInput input, ISender s, CancellationToken ct) => s.Send(new UpdateWorkOrder(id, input), ct)).RequirePermission(P.WorkOrdersEdit);
        g.MapPost("/{id:guid}/status", (Guid id, WorkOrderStatusBody body, ISender s, CancellationToken ct) =>
            s.Send(new ChangeWorkOrderStatus(id, body.Status, body.ActualCost, body.ResolutionNotes), ct)).RequireAuthorization(); // permission depends on the target status
    }

    private static void MapDeals(RouteGroupBuilder g)
    {
        g.MapPagedList<BrokerageDeal, DealDto>("", P.DealsRead, Docs.Deal, DealDto.From);
        g.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetDeal(id), ct)).RequirePermission(P.DealsRead);
        g.MapPost("", async (DealInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateDeal(input), ct);
            return Results.Created($"{Base}/deals/{created.Id}", created);
        }).RequirePermission(P.DealsCreate);
        g.MapPut("/{id:guid}", (Guid id, DealInput input, ISender s, CancellationToken ct) => s.Send(new UpdateDeal(id, input), ct)).RequirePermission(P.DealsEdit);
        g.MapPost("/{id:guid}/stage", (Guid id, DealStageBody body, ISender s, CancellationToken ct) => s.Send(new MoveDealStage(id, body.Stage, body.LostReason), ct)).RequireAuthorization();
    }
}
