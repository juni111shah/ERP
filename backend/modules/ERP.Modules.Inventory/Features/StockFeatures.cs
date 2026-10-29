using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

public sealed record ReceiveStockInput(Guid WarehouseId, Guid ItemId, decimal Quantity, decimal UnitCost, DateOnly Date, Guid OffsetAccountId, string? Reference = null, string? Memo = null);
public sealed record IssueStockInput(Guid WarehouseId, Guid ItemId, decimal Quantity, DateOnly Date, Guid? ExpenseAccountId = null, string? Reference = null, string? Memo = null);
public sealed record AdjustStockInput(Guid WarehouseId, Guid ItemId, decimal QuantityDelta, DateOnly Date, decimal UnitCost = 0, Guid? OffsetAccountId = null, string? Reference = null, string? Memo = null);
public sealed record TransferStockInput(Guid FromWarehouseId, Guid ToWarehouseId, Guid ItemId, decimal Quantity, DateOnly Date, string? Reference = null, string? Memo = null);
public sealed record StockPolicyInput(Guid ItemId, decimal ReorderLevel, decimal ReorderQuantity);

public sealed record MovementDto(Guid Id, Guid WarehouseId, Guid ItemId, DateOnly Date, MovementKind Kind, decimal Quantity, decimal UnitCost, string? Reference, string? Memo, Guid? JournalEntryId);
public sealed record StockRow(Guid WarehouseId, string Warehouse, Guid ItemId, string Sku, string Item, string Uom, decimal Quantity, decimal AverageCost, decimal Value);
public sealed record StockOnHandDto(IReadOnlyList<StockRow> Rows, decimal TotalValue);
public sealed record LowStockRow(Guid ItemId, string Sku, string Item, decimal OnHand, decimal ReorderLevel, decimal ReorderQuantity);

public sealed record GetStockOnHand(Guid? WarehouseId, Guid? ItemId) : IQuery<StockOnHandDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
public sealed record GetLowStock : IQuery<IReadOnlyList<LowStockRow>>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
public sealed record ReceiveStock(ReceiveStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
public sealed record IssueStock(IssueStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
public sealed record AdjustStock(AdjustStockInput Input) : ICommand<MovementDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
public sealed record TransferStock(TransferStockInput Input) : ICommand<IReadOnlyList<MovementDto>>, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }
public sealed record SetStockPolicy(StockPolicyInput Input) : ICommand, IRequirePermission { public string RequiredPermission => InventoryModule.StockWrite; }

public sealed class ReceiveStockValidator : AbstractValidator<ReceiveStock>
{
    public ReceiveStockValidator()
    {
        RuleFor(x => x.Input.Quantity).GreaterThan(0); RuleFor(x => x.Input.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty(); RuleFor(x => x.Input.OffsetAccountId).NotEmpty();
    }
}
public sealed class IssueStockValidator : AbstractValidator<IssueStock>
{
    public IssueStockValidator() { RuleFor(x => x.Input.Quantity).GreaterThan(0); RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty(); }
}
public sealed class AdjustStockValidator : AbstractValidator<AdjustStock>
{
    public AdjustStockValidator() { RuleFor(x => x.Input.QuantityDelta).NotEqual(0); RuleFor(x => x.Input.UnitCost).GreaterThanOrEqualTo(0); RuleFor(x => x.Input.WarehouseId).NotEmpty(); RuleFor(x => x.Input.ItemId).NotEmpty(); }
}
public sealed class TransferStockValidator : AbstractValidator<TransferStock>
{
    public TransferStockValidator()
    {
        RuleFor(x => x.Input.Quantity).GreaterThan(0);
        RuleFor(x => x.Input.ToWarehouseId).NotEqual(x => x.Input.FromWarehouseId).WithMessage("Choose two different warehouses.");
    }
}
public sealed class SetStockPolicyValidator : AbstractValidator<SetStockPolicy>
{
    public SetStockPolicyValidator() { RuleFor(x => x.Input.ItemId).NotEmpty(); RuleFor(x => x.Input.ReorderLevel).GreaterThanOrEqualTo(0); RuleFor(x => x.Input.ReorderQuantity).GreaterThanOrEqualTo(0); }
}

internal sealed class StockHandlers(IRepository<StockMovement> movements, IRepository<StockPolicy> policies, IReadRepository<Warehouse> warehouses, IReadRepository<Item> items, IReadRepository<ERP.Domain.Finance.Account> accounts,
    IPostingService posting, IUnitOfWork unitOfWork) :
    IRequestHandler<GetStockOnHand, StockOnHandDto>,
    IRequestHandler<GetLowStock, IReadOnlyList<LowStockRow>>,
    IRequestHandler<ReceiveStock, MovementDto>,
    IRequestHandler<IssueStock, MovementDto>,
    IRequestHandler<AdjustStock, MovementDto>,
    IRequestHandler<TransferStock, IReadOnlyList<MovementDto>>,
    IRequestHandler<SetStockPolicy, Unit>
{
    public async Task<StockOnHandDto> Handle(GetStockOnHand q, CancellationToken ct)
    {
        var all = await movements.Query(new Specification<StockMovement>(m => (q.WarehouseId == null || m.WarehouseId == q.WarehouseId) && (q.ItemId == null || m.ItemId == q.ItemId))).ToListAsync(ct);
        var wIds = all.Select(m => m.WarehouseId).Distinct().ToList(); var iIds = all.Select(m => m.ItemId).Distinct().ToList();
        var wMap = await warehouses.Query(new Specification<Warehouse>(w => wIds.Contains(w.Id))).ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var iMap = await items.Query(new Specification<Item>(i => iIds.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name, i.Uom }).ToDictionaryAsync(i => i.Id, ct);
        var rows = all.GroupBy(m => (m.WarehouseId, m.ItemId)).Select(g => (g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0)
            .Select(x => new StockRow(x.Key.WarehouseId, wMap.GetValueOrDefault(x.Key.WarehouseId, ""), x.Key.ItemId, iMap.GetValueOrDefault(x.Key.ItemId)?.Sku ?? "?", iMap.GetValueOrDefault(x.Key.ItemId)?.Name ?? "(deleted item)", iMap.GetValueOrDefault(x.Key.ItemId)?.Uom ?? "", x.Pos.Quantity, x.Pos.AverageCost, x.Pos.Value))
            .OrderBy(r => r.Sku).ThenBy(r => r.Warehouse).ToList();
        return new(rows, rows.Sum(r => r.Value));
    }

    public async Task<IReadOnlyList<LowStockRow>> Handle(GetLowStock q, CancellationToken ct)
    {
        var rules = await policies.Query().ToListAsync(ct);
        if (rules.Count == 0) return [];
        var ids = rules.Select(r => r.ItemId).ToList();
        var qty = (await movements.Query(new Specification<StockMovement>(m => ids.Contains(m.ItemId))).Select(m => new { m.ItemId, m.Quantity }).ToListAsync(ct)).GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
        var map = await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name }).ToDictionaryAsync(i => i.Id, ct);
        return rules.Where(r => qty.GetValueOrDefault(r.ItemId) <= r.ReorderLevel).Select(r => new LowStockRow(r.ItemId, map.GetValueOrDefault(r.ItemId)?.Sku ?? "?", map.GetValueOrDefault(r.ItemId)?.Name ?? "(deleted item)", qty.GetValueOrDefault(r.ItemId), r.ReorderLevel, r.ReorderQuantity)).OrderBy(r => r.Sku).ToList();
    }

    public Task<MovementDto> Handle(ReceiveStock cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var (wh, _) = await RequireAsync(i.WarehouseId, i.ItemId, token);
        await RequireAccountAsync(i.OffsetAccountId, "offsetAccountId", token);
        var m = New(wh, i.ItemId, i.Date, MovementKind.Receipt, i.Quantity, i.UnitCost, i.Reference, i.Memo);
        var value = Math.Round(i.Quantity * i.UnitCost, 2);
        if (value > 0)
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, i.Memo ?? "Stock received", i.Reference,
                [new PostingLine(await posting.AccountForRoleAsync("inventory", token), value, 0, "Stock received"), new PostingLine(i.OffsetAccountId, 0, value, "Stock received")]), token)).Id;
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return ToDto(m);
    }, ct);

    public Task<MovementDto> Handle(IssueStock cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var (wh, _) = await RequireAsync(i.WarehouseId, i.ItemId, token);
        var position = await PositionAsync(i.WarehouseId, i.ItemId, token, i.Date);
        if (position.Quantity < i.Quantity) throw new ConflictException($"Only {position.Quantity:0.####} in stock; cannot issue {i.Quantity:0.####}.");
        var cost = position.AverageCost;
        var m = New(wh, i.ItemId, i.Date, MovementKind.Issue, -i.Quantity, cost, i.Reference, i.Memo);
        var value = Math.Round(i.Quantity * cost, 2);
        if (value > 0)
        {
            var expense = i.ExpenseAccountId ?? await posting.AccountForRoleAsync("cogs", token);
            await RequireAccountAsync(expense, "expenseAccountId", token);
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, i.Memo ?? "Stock issued", i.Reference,
                [new PostingLine(expense, value, 0, "Stock issued"), new PostingLine(await posting.AccountForRoleAsync("inventory", token), 0, value, "Stock issued")]), token)).Id;
        }
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return ToDto(m);
    }, ct);

    public Task<MovementDto> Handle(AdjustStock cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var (wh, _) = await RequireAsync(i.WarehouseId, i.ItemId, token);
        var position = await PositionAsync(i.WarehouseId, i.ItemId, token, i.Date);
        if (i.QuantityDelta < 0 && position.Quantity < -i.QuantityDelta) throw new ConflictException($"Only {position.Quantity:0.####} in stock; cannot reduce by {-i.QuantityDelta:0.####}.");
        var cost = i.QuantityDelta > 0 ? i.UnitCost : position.AverageCost;
        var m = New(wh, i.ItemId, i.Date, MovementKind.Adjustment, i.QuantityDelta, cost, i.Reference, i.Memo ?? "Stock adjustment");
        var value = Math.Round(Math.Abs(i.QuantityDelta) * cost, 2);
        if (value > 0)
        {
            var offset = i.OffsetAccountId ?? await posting.AccountForRoleAsync("cogs", token);
            await RequireAccountAsync(offset, "offsetAccountId", token);
            var inv = await posting.AccountForRoleAsync("inventory", token);
            var lines = i.QuantityDelta > 0 ? new[] { new PostingLine(inv, value, 0), new PostingLine(offset, 0, value) } : [new PostingLine(offset, value, 0), new PostingLine(inv, 0, value)];
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, m.Memo, i.Reference, lines), token)).Id;
        }
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return ToDto(m);
    }, ct);

    public Task<IReadOnlyList<MovementDto>> Handle(TransferStock cmd, CancellationToken ct) => unitOfWork.InTransactionAsync<IReadOnlyList<MovementDto>>(async token =>
    {
        var i = cmd.Input;
        var (from, _) = await RequireAsync(i.FromWarehouseId, i.ItemId, token);
        var (to, _) = await RequireAsync(i.ToWarehouseId, i.ItemId, token);
        if (from.LegalEntityId != to.LegalEntityId) throw new ConflictException("Transfers are between warehouses of the same legal entity.");
        var position = await PositionAsync(i.FromWarehouseId, i.ItemId, token, i.Date);
        if (position.Quantity < i.Quantity) throw new ConflictException($"Only {position.Quantity:0.####} in stock at the source warehouse.");
        var transfer = Guid.NewGuid();
        var memo = i.Memo ?? $"Transfer {from.Code} → {to.Code}";
        var outM = New(from, i.ItemId, i.Date, MovementKind.TransferOut, -i.Quantity, position.AverageCost, i.Reference, memo); outM.TransferId = transfer;
        var inM = New(to, i.ItemId, i.Date, MovementKind.TransferIn, i.Quantity, position.AverageCost, i.Reference, memo); inM.TransferId = transfer;
        movements.AddRange([outM, inM]);
        await movements.SaveChangesAsync(token);
        return [ToDto(outM), ToDto(inM)];
    }, ct);

    public async Task<Unit> Handle(SetStockPolicy cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!await items.AnyAsync(x => x.Id == i.ItemId && x.Type == ItemType.Product, ct)) throw new FieldValidationException("itemId", "Choose a product item.");
        var existing = await policies.Tracked(new Specification<StockPolicy>(p => p.ItemId == i.ItemId)).FirstOrDefaultAsync(ct);
        if (existing is null) policies.Add(new StockPolicy { ItemId = i.ItemId, ReorderLevel = i.ReorderLevel, ReorderQuantity = i.ReorderQuantity });
        else { existing.ReorderLevel = i.ReorderLevel; existing.ReorderQuantity = i.ReorderQuantity; }
        await policies.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<(Warehouse Warehouse, Item Item)> RequireAsync(Guid warehouseId, Guid itemId, CancellationToken ct)
    {
        var wh = await warehouses.FindAsync(warehouseId, ct) ?? throw new FieldValidationException("warehouseId", "Warehouse not found.");
        if (!wh.IsActive) throw new ConflictException($"Warehouse {wh.Code} is not active.");
        var item = await items.FindAsync(itemId, ct) ?? throw new FieldValidationException("itemId", "Item not found.");
        if (item.Type != ItemType.Product) throw new FieldValidationException("itemId", "Only product items are kept in stock.");
        return (wh, item);
    }

    private async Task RequireAccountAsync(Guid accountId, string field, CancellationToken ct)
    {
        if (!await accounts.AnyAsync(a => a.Id == accountId && a.IsActive && a.IsPostable, ct)) throw new FieldValidationException(field, "Choose an active, postable account.");
    }

    /// <summary>Position of one item in one warehouse, optionally only up to a date (so a backdated issue cannot use stock that arrives later).</summary>
    private async Task<StockPosition> PositionAsync(Guid warehouseId, Guid itemId, CancellationToken ct, DateOnly? asOf = null)
        => StockCosting.Replay(await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == warehouseId && m.ItemId == itemId && (asOf == null || m.Date <= asOf))).ToListAsync(ct));

    private static StockMovement New(Warehouse wh, Guid itemId, DateOnly date, MovementKind kind, decimal qty, decimal cost, string? reference, string? memo)
        => new() { LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, ItemId = itemId, Date = date, Kind = kind, Quantity = qty, UnitCost = cost, Reference = reference, Memo = memo };

    private static MovementDto ToDto(StockMovement m) => new(m.Id, m.WarehouseId, m.ItemId, m.Date, m.Kind, m.Quantity, m.UnitCost, m.Reference, m.Memo, m.JournalEntryId);
}

/// <summary>Stock value and low-stock count on the overview.</summary>
public sealed class InventoryDashboard(ISender sender, ICurrentUser user) : ERP.Application.Dashboard.IDashboardContributor
{
    public string ModuleKey => InventoryModule.Key;

    public async Task<ERP.Application.Dashboard.DashboardContribution> BuildAsync(ERP.Application.Dashboard.DashboardScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(InventoryModule.StockRead)) return ERP.Application.Dashboard.DashboardContribution.Empty;
        var stock = await sender.Send(new GetStockOnHand(null, null), ct);
        var low = await sender.Send(new GetLowStock(), ct);
        var alerts = low.Count == 0 ? [] : new List<ERP.Application.Dashboard.DashboardAlert> { new("warning", "Inventory", "action", $"{low.Count} item(s) at or below their reorder level", "/inventory/stock", 40) };
        return new([new("stock-value", "Stock value", stock.TotalValue, "money", $"{stock.Rows.Count} item/warehouse lines", 120, "/inventory/stock")], [], alerts);
    }
}
