using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class SaveCountedQuantitiesHandler(IStockCountRepository counts, IRepository<StockCountLine> lineRows, IStockItemRepository items, StockCountDtoBuilder dtos)
    : ICommandHandler<SaveCountedQuantitiesCommand, StockCountDto>
{
    public async Task<StockCountDto> Handle(SaveCountedQuantitiesCommand cmd, CancellationToken ct)
    {
        // 1. Load the count; a posted or cancelled one is final.
        var count = await counts.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (count.Status is StockCountStatus.Posted or StockCountStatus.Cancelled) throw new ConflictException($"A {count.Status.ToString().ToLowerInvariant()} count can no longer be changed.");

        // 2. Enter each counted quantity; an item not on the sheet must be a product and gets a new line.
        var added = new List<StockCountLine>();
        var newIds = cmd.Lines.Select(l => l.ItemId).Where(id => count.Lines.All(l => l.ItemId != id)).Distinct().ToList();
        var newItems = newIds.Count == 0 ? [] : await items.MapByIdsAsync(newIds, ct); // one query for every new line
        foreach (var input in cmd.Lines)
        {
            var line = count.Lines.FirstOrDefault(l => l.ItemId == input.ItemId) ?? added.FirstOrDefault(l => l.ItemId == input.ItemId);
            if (line is null)
            {
                var item = newItems.GetValueOrDefault(input.ItemId) ?? throw new FieldValidationException("lines", "Item not found.");
                if (item.Type != ItemType.Product) throw new FieldValidationException("lines", $"{item.Sku} is not a stocked product.");
                line = new StockCountLine { StockCountId = count.Id, ItemId = item.Id, SystemQuantity = 0 };
                added.Add(line);
            }
            line.CountedQuantity = input.CountedQuantity;
        }
        lineRows.AddRange(added); // children with client GUIDs go through their own repository

        // 3. The count is Counting once anything is counted, and saved.
        count.Status = count.Lines.Union(added).Any(l => l.CountedQuantity is not null) ? StockCountStatus.Counting : StockCountStatus.Draft;
        await counts.SaveChangesAsync(ct);
        foreach (var a in added) if (!count.Lines.Contains(a)) count.Lines.Add(a); // EF may already have linked them
        return await dtos.ToDtoAsync(count, ct);
    }
}
