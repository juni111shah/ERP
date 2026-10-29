using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Distribution;

internal static class DistributionEndpoints
{
    private const string Base = "/api/v1/m/distribution";

    public static void Map(RouteGroupBuilder group)
    {
        // Routes & Stops
        var routes = group.MapGroup("/routes");
        routes.MapPagedList<DistributionRoute, DistributionRouteSummaryDto>("", Permissions.RoutesRead,
            r => new DistributionRouteSummaryDto(
                r.Id, r.LegalEntityId, r.Code, r.Name, r.DriverName, r.VehicleNumber,
                r.VehicleCapacityKg, r.WarehouseId, r.IsActive, r.Stops.Count, r.CreatedAt));
        routes.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var r = await s.Send(new GetRouteByIdQuery(id), ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).RequirePermission(Permissions.RoutesRead);
        routes.MapPost("", async (CreateRouteCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/routes/{created.Id}", created);
        }).RequirePermission(Permissions.RoutesManage);
        routes.MapPut("/{id:guid}", (Guid id, UpdateRouteCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.RoutesManage);
        routes.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteRouteCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.RoutesManage);

        // Van Sales Trips
        var trips = group.MapGroup("/trips");
        trips.MapPagedList<VanSalesTrip, VanSalesTripSummaryDto>("", Permissions.TripsRead,
            t => new VanSalesTripSummaryDto(
                t.Id, t.LegalEntityId, t.TripNumber, t.RouteId, t.RouteName,
                t.DriverName, t.VehicleNumber, t.DepartureDate, t.Status,
                t.TotalSalesAmount, t.TotalCollectionsAmount, t.InventoryItems.Count, t.CreatedAt));
        trips.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var t = await s.Send(new GetTripByIdQuery(id), ct);
            return t is null ? Results.NotFound() : Results.Ok(t);
        }).RequirePermission(Permissions.TripsRead);
        trips.MapPost("", async (CreateTripCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/trips/{created.Id}", created);
        }).RequirePermission(Permissions.TripsManage);
        trips.MapPost("/{id:guid}/complete", (Guid id, CompleteTripCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { TripId = id }, ct)).RequirePermission(Permissions.TripsManage);

        // Wholesale Price Matrices
        var wholesale = group.MapGroup("/wholesale");
        wholesale.MapPagedList<WholesalePriceMatrix, WholesalePriceMatrixDto>("", Permissions.PricingRead,
            w => new WholesalePriceMatrixDto(
                w.Id, w.LegalEntityId, w.ItemId, w.ItemCode, w.ItemName,
                w.Tier, w.MinQuantity, w.MaxQuantity, w.DiscountPercentage,
                w.WholesaleUnitPrice, w.IsActive, w.Notes, w.CreatedAt));
        wholesale.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var w = await s.Send(new GetWholesalePriceByIdQuery(id), ct);
            return w is null ? Results.NotFound() : Results.Ok(w);
        }).RequirePermission(Permissions.PricingRead);
        wholesale.MapPost("", async (CreateWholesalePriceCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/wholesale/{created.Id}", created);
        }).RequirePermission(Permissions.PricingManage);
        wholesale.MapPut("/{id:guid}", (Guid id, UpdateWholesalePriceCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.PricingManage);
        wholesale.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteWholesalePriceCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.PricingManage);
        wholesale.MapPost("/calculate-rate", (CalculateWholesaleRateQuery q, ISender s, CancellationToken ct) =>
            s.Send(q, ct)).RequirePermission(Permissions.PricingRead);

        // Fleet Analytics / KPIs
        group.MapGet("/kpis", (ISender s, CancellationToken ct) =>
            s.Send(new GetDistributionKpiSummaryQuery(), ct)).RequirePermission(Permissions.AnalyticsRead);
    }
}
