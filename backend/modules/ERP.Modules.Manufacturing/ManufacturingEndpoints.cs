using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Manufacturing;

public sealed record CancelWorkOrderBody(string? Reason);

internal static class ManufacturingEndpoints
{
    private const string Base = "/api/v1/m/manufacturing";

    public static void Map(RouteGroupBuilder group)
    {
        // Bills of Materials (BOM)
        var boms = group.MapGroup("/boms");
        boms.MapPagedList<BillOfMaterials, BillOfMaterialsSummaryDto>("", Permissions.BomsRead,
            b => new BillOfMaterialsSummaryDto(
                b.Id, b.LegalEntityId, b.Code, b.Name, b.ProducedItemCode, b.ProducedItemName,
                b.OutputQuantity, b.Uom, b.Version,
                b.OutputQuantity > 0 ? ((b.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m))) / b.OutputQuantity) + b.LaborCostPerUnit + b.OverheadCostPerUnit) : 0,
                b.IsActive, b.Components.Count, b.CreatedAt));
        boms.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var b = await s.Send(new GetBomByIdQuery(id), ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).RequirePermission(Permissions.BomsRead);
        boms.MapPost("", async (CreateBomCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/boms/{created.Id}", created);
        }).RequirePermission(Permissions.BomsManage);
        boms.MapPut("/{id:guid}", (Guid id, UpdateBomCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.BomsManage);
        boms.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteBomCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.BomsManage);

        // Production Work Orders
        var orders = group.MapGroup("/workorders");
        orders.MapPagedList<WorkOrder, WorkOrderSummaryDto>("", Permissions.WorkOrdersRead,
            w => new WorkOrderSummaryDto(
                w.Id, w.LegalEntityId, w.WorkOrderNumber, w.BomCode, w.ProducedItemCode, w.ProducedItemName,
                w.PlannedQuantity, w.CompletedQuantity, w.Uom, w.DueDate, w.Status,
                w.EstimatedTotalCost, w.ActualTotalCost, w.CreatedAt));
        orders.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var w = await s.Send(new GetWorkOrderByIdQuery(id), ct);
            return w is null ? Results.NotFound() : Results.Ok(w);
        }).RequirePermission(Permissions.WorkOrdersRead);
        orders.MapPost("", async (CreateWorkOrderCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/workorders/{created.Id}", created);
        }).RequirePermission(Permissions.WorkOrdersManage);
        orders.MapPost("/{id:guid}/release", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new ReleaseWorkOrderCommand(id), ct)).RequirePermission(Permissions.WorkOrdersManage);
        orders.MapPost("/{id:guid}/complete", (Guid id, CompleteWorkOrderCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.WorkOrdersManage);
        orders.MapPost("/{id:guid}/cancel", (Guid id, CancelWorkOrderBody body, ISender s, CancellationToken ct) =>
            s.Send(new CancelWorkOrderCommand(id, body.Reason), ct)).RequirePermission(Permissions.WorkOrdersManage);

        // Product Costing & Variance
        var costing = group.MapGroup("/costing");
        costing.MapGet("/rollup/{bomId:guid}", (Guid bomId, ISender s, CancellationToken ct) =>
            s.Send(new CalculateProductCostingRollupQuery(bomId), ct)).RequirePermission(Permissions.CostingRead);
        costing.MapGet("/variance", (Guid legalEntityId, Guid itemId, ISender s, CancellationToken ct) =>
            s.Send(new GetProductCostVarianceQuery(legalEntityId, itemId), ct)).RequirePermission(Permissions.CostingRead);

        // Manufacturing KPIs
        group.MapGet("/kpis", (ISender s, CancellationToken ct) =>
            s.Send(new GetManufacturingKpiSummaryQuery(), ct)).RequirePermission(Permissions.AnalyticsRead);
    }
}
