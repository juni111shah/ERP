using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Retail;

internal static class RetailEndpoints
{
    private const string Base = "/api/v1/m/retail";

    public static void Map(RouteGroupBuilder group)
    {
        // Stores
        var stores = group.MapGroup("/stores");
        stores.MapPagedList<RetailStore, RetailStoreDto>("", Permissions.StoresRead,
            s => new RetailStoreDto(
                s.Id, s.LegalEntityId, s.Code, s.Name, s.WarehouseId, s.StoreManagerName,
                s.Phone, s.Email, s.Address, s.IsActive, s.CustomFields, s.CreatedAt));
        stores.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var store = await s.Send(new GetStoreByIdQuery(id), ct);
            return store is null ? Results.NotFound() : Results.Ok(store);
        }).RequirePermission(Permissions.StoresRead);
        stores.MapPost("", async (CreateStoreCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/stores/{created.Id}", created);
        }).RequirePermission(Permissions.StoresManage);
        stores.MapPut("/{id:guid}", (Guid id, UpdateStoreCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.StoresManage);
        stores.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeleteStoreCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.StoresManage);

        // Price Lists
        var priceLists = group.MapGroup("/pricelists");
        priceLists.MapPagedList<RetailPriceList, RetailPriceListSummaryDto>("", Permissions.PriceListsRead,
            p => new RetailPriceListSummaryDto(
                p.Id, p.LegalEntityId, p.Code, p.Name, p.Type, p.Currency,
                p.ValidFrom, p.ValidTo, p.IsDefault, p.IsActive, p.Items.Count, p.CreatedAt));
        priceLists.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
            s.Send(new GetPriceListByIdQuery(id), ct)).RequirePermission(Permissions.PriceListsRead);
        priceLists.MapPost("", async (CreatePriceListCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/pricelists/{created.Id}", created);
        }).RequirePermission(Permissions.PriceListsManage);
        priceLists.MapPut("/{id:guid}", (Guid id, UpdatePriceListCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.PriceListsManage);
        priceLists.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeletePriceListCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.PriceListsManage);

        // Promotions
        var promotions = group.MapGroup("/promotions");
        promotions.MapPagedList<RetailPromotion, RetailPromotionDto>("", Permissions.PromotionsRead,
            p => new RetailPromotionDto(
                p.Id, p.LegalEntityId, p.Code, p.Name, p.Type, p.DiscountValue,
                p.MinimumOrderAmount, p.StartDate, p.EndDate, p.UsageLimit,
                p.TimesUsed, p.IsActive, p.Description, p.CustomFields, p.CreatedAt));
        promotions.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            var promo = await s.Send(new GetPromotionByIdQuery(id), ct);
            return promo is null ? Results.NotFound() : Results.Ok(promo);
        }).RequirePermission(Permissions.PromotionsRead);
        promotions.MapPost("", async (CreatePromotionCommand cmd, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(cmd, ct);
            return Results.Created($"{Base}/promotions/{created.Id}", created);
        }).RequirePermission(Permissions.PromotionsManage);
        promotions.MapPut("/{id:guid}", (Guid id, UpdatePromotionCommand cmd, ISender s, CancellationToken ct) =>
            s.Send(cmd with { Id = id }, ct)).RequirePermission(Permissions.PromotionsManage);
        promotions.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
        {
            await s.Send(new DeletePromotionCommand(id), ct);
            return Results.NoContent();
        }).RequirePermission(Permissions.PromotionsManage);
        promotions.MapPost("/validate", (ValidatePromoCodeQuery query, ISender s, CancellationToken ct) =>
            s.Send(query, ct)).RequirePermission(Permissions.PromotionsRead);

        // Analytics / KPIs
        group.MapGet("/kpis", (ISender s, CancellationToken ct) =>
            s.Send(new GetRetailKpiSummaryQuery(), ct)).RequirePermission(Permissions.AnalyticsRead);
    }
}
