using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class ConsumeRecordHandler(IIndustryRecordRepository records, IStockPostingService stock, IPostingService posting, RecordDtoBuilder dtos, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ConsumeRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(ConsumeRecordCommand cmd, CancellationToken ct)
    {
        // 1. The type must use stock; load the record, not issued yet and with stock lines.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (!def.ConsumesStock) throw new ConflictException($"{def.Label} do not use stock.");
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        if (record.ConsumedOn is not null) throw new ConflictException("These materials have already been issued.");
        var lines = record.Lines.Where(l => l.ItemId is not null && l.Quantity > 0).ToList();
        if (lines.Count == 0) throw new ConflictException("Add a line with a stock item first.");

        // 2. Inventory on, a usable warehouse, and enough on hand of every item.
        if (!await stock.IsInventoryEnabledAsync(ct)) throw new ConflictException("Turn on the Inventory module to issue materials.");
        if (cmd.WarehouseId is { } w && !await stock.IsWarehouseUsableAsync(w, record.LegalEntityId, ct)) throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");

        var needed = lines.GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var onHand = await stock.GetOnHandAsync(needed.Keys, cmd.WarehouseId, record.LegalEntityId, ct);
        foreach (var (itemId, quantity) in needed)
            if (onHand.GetValueOrDefault(itemId) < quantity)
                throw new ConflictException($"Not enough stock of '{lines.First(l => l.ItemId == itemId).Description}': {onHand.GetValueOrDefault(itemId):0.##} on hand, {quantity:0.##} needed.");

        // 3. Issue every line and mark the record, all or nothing.
        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var account = await posting.AccountForRoleAsync(def.ConsumeAccountRole ?? "cogs", ct);
        return await unitOfWork.InTransactionAsync(async _ =>
        {
            foreach (var line in lines)
                await stock.IssueStockAsync(new StockIssueRequest(record.LegalEntityId, line.ItemId!.Value, line.Quantity, date, record.Number, $"{def.Singular} {record.Number}: {line.Description}", cmd.WarehouseId, account), ct);
            record.ConsumedOn = date;
            await records.SaveChangesAsync(ct);
            return dtos.ToDto(record, def, record.Lines);
        }, ct);
    }
}
