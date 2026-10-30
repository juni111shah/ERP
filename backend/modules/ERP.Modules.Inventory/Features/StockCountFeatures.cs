using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

public sealed record StockCountInput(Guid WarehouseId, DateOnly CountDate, string? Memo = null);
public sealed record CountedLineInput(Guid ItemId, decimal? CountedQuantity);
public sealed record StockCountLineDto(Guid ItemId, string Sku, string Item, string Uom, decimal SystemQuantity, decimal? CountedQuantity, decimal? Variance, Guid? MovementId);
public sealed record StockCountDto(Guid Id, string Number, Guid WarehouseId, string Warehouse, DateOnly CountDate, StockCountStatus Status, string? Memo, DateTime? PostedAt, IReadOnlyList<StockCountLineDto> Lines);
public sealed record StockCountRow(Guid Id, string Number, Guid WarehouseId, DateOnly CountDate, StockCountStatus Status, int Lines, string? Memo);

public sealed record GetStockCount(Guid Id) : IQuery<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsRead; }
/// <summary>Starts a count sheet for a warehouse: one line per item currently held there, with the system quantity.</summary>
public sealed record CreateStockCount(StockCountInput Input) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
/// <summary>Enters (or clears) counted quantities; an item not on the sheet is added as unexpected stock with system quantity 0.</summary>
public sealed record SaveCountedQuantities(Guid Id, IReadOnlyList<CountedLineInput> Lines) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
/// <summary>Books one adjustment per counted difference. Posting a posted count again changes nothing.</summary>
public sealed record PostStockCount(Guid Id) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }
public sealed record CancelStockCount(Guid Id) : ICommand<StockCountDto>, IRequirePermission { public string RequiredPermission => InventoryModule.CountsManage; }

public sealed class CreateStockCountValidator : AbstractValidator<CreateStockCount>
{
    public CreateStockCountValidator() { RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.Memo).MaximumLength(500); }
}
public sealed class SaveCountedQuantitiesValidator : AbstractValidator<SaveCountedQuantities>
{
    public SaveCountedQuantitiesValidator()
    {
        RuleFor(x => x.Lines).NotNull().Must(l => l is null || l.Count <= 2000).WithMessage("At most 2000 lines.");
        RuleForEach(x => x.Lines).ChildRules(l => { l.RuleFor(x => x.ItemId).NotEmpty(); l.RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0).When(x => x.CountedQuantity is not null); });
    }
}

internal sealed class StockCountHandlers(IRepository<StockCount> counts, IRepository<StockCountLine> lineRows, IRepository<StockMovement> movements, IReadRepository<Warehouse> warehouses, IReadRepository<Item> items,
    IPostingService posting, IUnitOfWork unitOfWork, IClock clock) :
    IRequestHandler<GetStockCount, StockCountDto>,
    IRequestHandler<CreateStockCount, StockCountDto>,
    IRequestHandler<SaveCountedQuantities, StockCountDto>,
    IRequestHandler<PostStockCount, StockCountDto>,
    IRequestHandler<CancelStockCount, StockCountDto>
{
    public async Task<StockCountDto> Handle(GetStockCount q, CancellationToken ct) => await ToDtoAsync(await LoadAsync(q.Id, false, ct), ct);

    public async Task<StockCountDto> Handle(CreateStockCount cmd, CancellationToken ct)
    {
        var wh = await warehouses.FindAsync(cmd.Input.WarehouseId, ct) ?? throw new FieldValidationException("warehouseId", "Warehouse not found.");
        if (!wh.IsActive) throw new ConflictException($"Warehouse {wh.Code} is not active.");
        var number = $"CNT-{(await counts.CountAsync(null, ct)) + 1:D5}";
        var count = new StockCount { LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, CountDate = cmd.Input.CountDate, Memo = cmd.Input.Memo, Number = number };
        var positions = (await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == wh.Id)).ToListAsync(ct)).GroupBy(m => m.ItemId).Select(g => (Item: g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0);
        count.Lines.AddRange(positions.Select(x => new StockCountLine { StockCountId = count.Id, ItemId = x.Item, SystemQuantity = x.Pos.Quantity }));
        counts.Add(count);
        await counts.SaveChangesAsync(ct);
        return await ToDtoAsync(count, ct);
    }

    public async Task<StockCountDto> Handle(SaveCountedQuantities cmd, CancellationToken ct)
    {
        var count = await LoadAsync(cmd.Id, true, ct);
        if (count.Status is StockCountStatus.Posted or StockCountStatus.Cancelled) throw new ConflictException($"A {count.Status.ToString().ToLowerInvariant()} count can no longer be changed.");
        var added = new List<StockCountLine>();
        foreach (var input in cmd.Lines)
        {
            var line = count.Lines.FirstOrDefault(l => l.ItemId == input.ItemId);
            if (line is null)
            {
                var item = await items.FindAsync(input.ItemId, ct) ?? throw new FieldValidationException("lines", "Item not found.");
                if (item.Type != ItemType.Product) throw new FieldValidationException("lines", $"{item.Sku} is not a stocked product.");
                line = new StockCountLine { StockCountId = count.Id, ItemId = item.Id, SystemQuantity = 0 };
                added.Add(line);
            }
            line.CountedQuantity = input.CountedQuantity;
        }
        lineRows.AddRange(added); // children with client GUIDs go through their own repository
        count.Status = count.Lines.Union(added).Any(l => l.CountedQuantity is not null) ? StockCountStatus.Counting : StockCountStatus.Draft;
        await counts.SaveChangesAsync(ct);
        foreach (var a in added) if (!count.Lines.Contains(a)) count.Lines.Add(a); // EF may already have linked them
        return await ToDtoAsync(count, ct);
    }

    public Task<StockCountDto> Handle(PostStockCount cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var count = await LoadAsync(cmd.Id, true, token);
        if (count.Status == StockCountStatus.Posted) return await ToDtoAsync(count, token); // idempotent
        if (count.Status == StockCountStatus.Cancelled) throw new ConflictException("A cancelled count cannot be posted.");
        if (count.Lines.All(l => l.CountedQuantity is null)) throw new ConflictException("Enter at least one counted quantity before posting.");
        var wh = await warehouses.GetAsync(count.WarehouseId, token);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        foreach (var line in count.Lines.Where(l => l.CountedQuantity is not null && l.MovementId is null))
        {
            var position = StockCosting.Replay(await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == count.WarehouseId && m.ItemId == line.ItemId)).ToListAsync(token));
            var delta = line.CountedQuantity!.Value - position.Quantity;
            if (delta == 0) continue;
            var cost = position.AverageCost;
            var movement = new StockMovement
            {
                LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, ItemId = line.ItemId, Date = count.CountDate > today ? today : count.CountDate, Kind = MovementKind.Adjustment, Quantity = delta,
                UnitCost = cost, Reference = count.Number, Memo = $"Stock count {count.Number}",
            };
            var value = Math.Round(Math.Abs(delta) * cost, 2);
            if (value > 0)
            {
                var inv = await posting.AccountForRoleAsync("inventory", token);
                var offset = await posting.AccountForRoleAsync("cogs", token);
                var lines = delta > 0 ? new[] { new PostingLine(inv, value, 0), new PostingLine(offset, 0, value) } : [new PostingLine(offset, value, 0), new PostingLine(inv, 0, value)];
                movement.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, movement.Date, "inventory", movement.Id, movement.Memo, count.Number, lines), token)).Id;
            }
            movements.Add(movement);
            line.MovementId = movement.Id;
        }
        count.Status = StockCountStatus.Posted;
        count.PostedAt = clock.UtcNow;
        await counts.SaveChangesAsync(token);
        return await ToDtoAsync(count, token);
    }, ct);

    public async Task<StockCountDto> Handle(CancelStockCount cmd, CancellationToken ct)
    {
        var count = await LoadAsync(cmd.Id, true, ct);
        if (count.Status == StockCountStatus.Posted) throw new ConflictException("A posted count cannot be cancelled.");
        count.Status = StockCountStatus.Cancelled;
        await counts.SaveChangesAsync(ct);
        return await ToDtoAsync(count, ct);
    }

    private async Task<StockCount> LoadAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var spec = new Specification<StockCount>(c => c.Id == id).Include(c => c.Lines);
        return await (tracked ? counts.Tracked(spec) : counts.Query(spec)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
    }

    private async Task<StockCountDto> ToDtoAsync(StockCount c, CancellationToken ct)
    {
        var wh = await warehouses.GetAsync(c.WarehouseId, ct);
        var ids = c.Lines.Select(l => l.ItemId).ToList();
        var map = await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name, i.Uom }).ToDictionaryAsync(i => i.Id, ct);
        var lines = c.Lines.Select(l => new { l, i = map.GetValueOrDefault(l.ItemId) }).OrderBy(x => x.i?.Sku)
            .Select(x => new StockCountLineDto(x.l.ItemId, x.i?.Sku ?? "?", x.i?.Name ?? "(deleted item)", x.i?.Uom ?? "", x.l.SystemQuantity, x.l.CountedQuantity, x.l.CountedQuantity is { } q ? q - x.l.SystemQuantity : null, x.l.MovementId)).ToList();
        return new(c.Id, c.Number, c.WarehouseId, wh.Name, c.CountDate, c.Status, c.Memo, c.PostedAt, lines);
    }
}
