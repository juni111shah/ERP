using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

internal sealed class StoresAssistantReader(IReadRepository<RetailStore> stores) : IAssistantReader
{
    public string Key => "retail_stores";
    public string Description => "Retail stores: code, name, manager and whether active.";
    public string? ModuleKey => RetailModule.Key;
    public string Permission => Permissions.StoresRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = stores.Query();
        if (search is not null) query = query.Where(s => s.Code.Contains(search) || s.Name.Contains(search));
        return await query.OrderBy(s => s.Code).Take(take).Select(s => new { s.Code, s.Name, Manager = s.StoreManagerName, s.IsActive, Link = "/retail/stores" }).ToListAsync(ct);
    }
}

internal sealed class PromotionsAssistantReader(IReadRepository<RetailPromotion> promotions, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "retail_promotions";
    public string Description => "Retail promotions, newest first: promo code, name, type, discount, dates, times used and whether active.";
    public string? ModuleKey => RetailModule.Key;
    public string Permission => Permissions.PromotionsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = promotions.Query();
        if (search is not null) query = query.Where(p => p.Code.Contains(search) || p.Name.Contains(search));
        return await query.OrderByDescending(p => p.StartDate).Take(take).Select(p => new { p.Code, p.Name, Type = p.Type.ToString(), p.DiscountValue, Currency = entities.Query(null).Where(e => e.Id == p.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), p.MinimumOrderAmount, p.StartDate, p.EndDate, p.TimesUsed, p.UsageLimit, p.IsActive, Link = "/retail/promotions" }).ToListAsync(ct);
    }
}

internal static class RetailAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Add a store", "Go to Retail > Stores, press Add Store, fill it in and press Save.", "/retail/stores", Permissions.StoresRead),
        new("Create a promotion", "Go to Retail > Promotions, press Create Promotion, fill it in and press Save. Test Promo Code tries a code out.", "/retail/promotions", Permissions.PromotionsRead),
        new("Set up a price list", "Go to Retail > Price lists, press Create Price List, fill it in and press Save. Open the list and press Add item to price its items.", "/retail/pricelists", Permissions.PriceListsRead),
    ];
}
