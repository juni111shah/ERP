using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class IssueStockHandler(IStockMovementRepository movements, StockMovementChecks checks, IPostingService posting, IUnitOfWork unitOfWork) : ICommandHandler<IssueStockCommand, MovementDto>
{
    public Task<MovementDto> Handle(IssueStockCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Check the warehouse and item, and that the stock is there on the date.
        var i = cmd.Input;
        var (wh, _) = await checks.RequireAsync(i.WarehouseId, i.ItemId, token);
        var position = await checks.PositionAsync(i.WarehouseId, i.ItemId, token, i.Date);
        if (position.Quantity < i.Quantity) throw new ConflictException($"Only {position.Quantity:0.####} in stock; cannot issue {i.Quantity:0.####}.");

        // 2. The issue at the average cost; it must not starve later movements.
        var cost = position.AverageCost;
        var m = StockMovementChecks.New(wh, i.ItemId, i.Date, MovementKind.Issue, -i.Quantity, cost, i.Reference, i.Memo);
        await checks.EnsureNoShortfallAsync(m, token);

        // 3. A valued issue is posted to the expense account against inventory.
        var value = Math.Round(i.Quantity * cost, 2);
        if (value > 0)
        {
            var expense = i.ExpenseAccountId ?? await posting.AccountForRoleAsync("cogs", token);
            await checks.RequireAccountAsync(expense, "expenseAccountId", token);
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, i.Memo ?? "Stock issued", i.Reference,
                [new PostingLine(expense, value, 0, "Stock issued"), new PostingLine(await posting.AccountForRoleAsync("inventory", token), 0, value, "Stock issued")]), token)).Id;
        }

        // 4. Save.
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return MovementDto.From(m);
    }, ct);
}
