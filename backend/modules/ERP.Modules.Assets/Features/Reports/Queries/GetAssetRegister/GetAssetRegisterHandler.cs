using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetAssetRegisterHandler(IFixedAssetRepository assets, IAssetCategoryRepository categories, IDepreciationEntryRepository entries, IReportCurrencyGuard currencies)
    : IQueryHandler<GetAssetRegisterQuery, AssetRegisterDto>
{
    public async Task<AssetRegisterDto> Handle(GetAssetRegisterQuery q, CancellationToken ct)
    {
        // 1. One currency only; the assets on the books, what was charged on them by the date, and the category names.
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var onBooks = await assets.ListOnBooksAsync(q.AsOf, q.LegalEntityId, q.CategoryId, ct);
        var ids = onBooks.Select(a => a.Id).ToList();
        var charged = await entries.ChargedByAssetAsync(ids, q.AsOf, ct);
        var names = await categories.MapNamesAsync(ct);

        // 2. Each asset at cost less what was charged.
        var rows = onBooks.Select(a =>
        {
            var accumulated = charged.GetValueOrDefault(a.Id);
            return new AssetRegisterRow(a.Id, a.LegalEntityId, a.Number, a.Name, a.CategoryId, names.GetValueOrDefault(a.CategoryId, "?"), a.BusinessUnitId, a.Custodian, a.AcquisitionDate, a.CapitalisedOn!.Value, a.Cost, a.SalvageValue, accumulated, a.Cost - accumulated);
        }).ToList();
        return new AssetRegisterDto(q.AsOf, rows, rows.Sum(r => r.Cost), rows.Sum(r => r.AccumulatedDepreciation), rows.Sum(r => r.NetBookValue));
    }
}
