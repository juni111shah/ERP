using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetStockGlReconciliationHandler(IStockMovementRepository movements, IAccountRepository accounts, IJournalEntryRepository journals, ILegalEntityRepository legalEntities)
    : IQueryHandler<GetStockGlReconciliationQuery, StockGlReconciliationDto>
{
    public async Task<StockGlReconciliationDto> Handle(GetStockGlReconciliationQuery q, CancellationToken ct)
    {
        // 1. Stock value per legal entity, from the movements replayed up to the date.
        var inventoryAccounts = await accounts.ListIdsWithSystemRoleAsync("inventory", ct);
        var moves = await movements.ListUpToForLegalEntityAsync(q.AsOf, q.LegalEntityId, ct);
        var stock = moves.GroupBy(m => m.LegalEntityId).ToDictionary(g => g.Key, g => g.GroupBy(m => (m.WarehouseId, m.ItemId)).Sum(p => StockCosting.Replay(p).Value));

        // 2. The inventory account balance per legal entity.
        // Reversed entries and their reversals both count, as in the trial balance. Added up here because SQLite (tests) cannot sum decimals.
        var ledger = (await journals.ListPostedEntityAmountsAsync(inventoryAccounts, q.AsOf, q.LegalEntityId, ct))
            .GroupBy(l => l.LegalEntityId).ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));

        // 3. One row per legal entity with either (and the asked one even without both).
        var ids = stock.Keys.Union(ledger.Keys).ToList();
        if (q.LegalEntityId is { } one && !ids.Contains(one)) ids.Add(one);
        var names = await legalEntities.MapNamesAndCurrenciesAsync(ids, ct);
        var rows = ids.Select(id =>
        {
            var value = Math.Round(stock.GetValueOrDefault(id), 2);
            var balance = ledger.GetValueOrDefault(id);
            return new StockGlReconciliationRow(id, names.GetValueOrDefault(id)?.Name ?? "?", names.GetValueOrDefault(id)?.BaseCurrency ?? "", value, balance, value - balance);
        }).OrderBy(r => r.LegalEntity).ToList();
        return new(q.AsOf, rows, rows.All(r => r.Difference == 0));
    }
}
