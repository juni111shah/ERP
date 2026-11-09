using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Catalog;

namespace ERP.Application.Catalog;

internal sealed class UpdateItemHandler(IRepository<Item> items, ICustomFieldService fields) : ICommandHandler<UpdateItemCommand, ItemDto>
{
    public async Task<ItemDto> Handle(UpdateItemCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed SKU must still be unique.
        var i = command.Input;
        var item = await items.GetForUpdateAsync(command.Id, ct);
        var sku = i.Sku.Trim();
        if (sku != item.Sku && await items.AnyAsync(x => x.Sku == sku, ct)) throw new ConflictException("An item with that SKU exists.");

        // 2. Apply the input.
        item.Sku = sku; item.Name = i.Name.Trim(); item.Type = i.Type; item.Uom = i.Uom; item.SalesPrice = i.SalesPrice;
        item.PurchasePrice = i.PurchasePrice; item.TaxCodeId = i.TaxCodeId; item.IsActive = i.IsActive;
        // Null leaves it alone, like the barcode.
        if (i.Category is not null) item.Category = i.Category.Trim() is { Length: > 0 } category ? category : null;
        // Null leaves it alone (callers that do not know about barcodes); an empty string clears it.
        if (i.Barcode is not null) item.Barcode = await ItemRules.CheckedBarcodeAsync(items, i.Barcode, item.Id, ct);

        // 3. Validate the custom fields and save.
        await fields.ApplyAsync(ItemRules.EntityType, item, i.CustomFields, isCreate: false, ct);
        await items.SaveChangesAsync(ct);
        return ItemDto.From(item, await fields.ReadAsync(ItemRules.EntityType, item, ct));
    }
}
