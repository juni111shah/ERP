using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;
using MediatR;

namespace ERP.Modules.Inventory.Features;

internal sealed class SetStockPolicyHandler(IStockPolicyRepository policies, IStockItemRepository items) : ICommandHandler<SetStockPolicyCommand>
{
    public async Task<Unit> Handle(SetStockPolicyCommand cmd, CancellationToken ct)
    {
        // 1. Only product items have a reorder rule.
        var i = cmd.Input;
        if (!await items.AnyAsync(x => x.Id == i.ItemId && x.Type == ItemType.Product, ct)) throw new FieldValidationException("itemId", "Choose a product item.");

        // 2. Create or update the item's rule and save.
        var existing = await policies.FindForItemForUpdateAsync(i.ItemId, ct);
        if (existing is null) policies.Add(new StockPolicy { ItemId = i.ItemId, ReorderLevel = i.ReorderLevel, ReorderQuantity = i.ReorderQuantity });
        else { existing.ReorderLevel = i.ReorderLevel; existing.ReorderQuantity = i.ReorderQuantity; }
        await policies.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
