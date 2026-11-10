using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosCatalogHandler(
    IRepository<PosRegister> registers,
    IPosItemRepository items,
    IPosTaxCodeRepository taxCodes,
    IStockPostingService stock,
    IEnumerable<IPriceResolver> priceResolvers) : IQueryHandler<GetPosCatalogQuery, PosCatalogPageDto>
{
    public async Task<PosCatalogPageDto> Handle(GetPosCatalogQuery request, CancellationToken ct)
    {
        // 1. The page of active items.
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var page = Math.Max(request.Page, 1);
        var code = request.Code?.Trim();
        var search = request.Search?.Trim();

        var (rows, total) = await items.PageActiveAsync(code, search, page, pageSize, ct);

        // 2. Their tax rates.
        var taxIds = rows.Where(i => i.TaxCodeId.HasValue).Select(i => i.TaxCodeId!.Value).Distinct().ToList();
        var rates = taxIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await taxCodes.RatesAsync(taxIds, ct);

        // 3. Stock and the price list price at the register, when one is named.
        var onHand = (IReadOnlyDictionary<Guid, decimal>)new Dictionary<Guid, decimal>();
        var prices = (IReadOnlyDictionary<Guid, decimal>)new Dictionary<Guid, decimal>();
        if (request.RegisterId is { } registerId && await registers.FindAsync(registerId, ct) is { } register)
        {
            onHand = await stock.GetOnHandAsync(rows.Select(i => i.Id).ToList(), register.WarehouseId, register.LegalEntityId, ct);
            if (priceResolvers.FirstOrDefault() is { } resolver) // for one unit; quantity breaks and customer prices come from /orders/quote
                prices = await resolver.GetPricesAsync(new PriceQuery(register.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), null, rows.ToDictionary(i => i.Id, _ => 1m)), ct);
        }

        return new PosCatalogPageDto(rows.Select(i => new PosCatalogItemDto(
            i.Id, i.Sku, i.Barcode, i.Name, i.Uom, i.Type, prices.TryGetValue(i.Id, out var listed) ? listed : i.SalesPrice,
            i.TaxCodeId is { } t && rates.TryGetValue(t, out var rate) ? rate : 0m,
            onHand.TryGetValue(i.Id, out var q) ? q : null)).ToList(), total);
    }
}
