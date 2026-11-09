using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Catalog;

namespace ERP.Application.Catalog;

internal sealed class CreateItemHandler(IRepository<Item> items, ICustomFieldService fields) : ICommandHandler<CreateItemCommand, ItemDto>
{
    public async Task<ItemDto> Handle(CreateItemCommand command, CancellationToken ct)
    {
        // 1. The SKU is unique.
        var i = command.Input;
        var sku = i.Sku.Trim();
        if (await items.AnyAsync(x => x.Sku == sku, ct)) throw new ConflictException("An item with that SKU exists.");

        // 2. Build it; the barcode is unique too.
        var item = new Item { Sku = sku, Name = i.Name.Trim(), Type = i.Type, Uom = i.Uom, SalesPrice = i.SalesPrice, PurchasePrice = i.PurchasePrice, TaxCodeId = i.TaxCodeId, IsActive = i.IsActive, Category = string.IsNullOrWhiteSpace(i.Category) ? null : i.Category.Trim() };
        item.Barcode = await ItemRules.CheckedBarcodeAsync(items, i.Barcode, item.Id, ct);

        // 3. Validate the custom fields and save.
        await fields.ApplyAsync(ItemRules.EntityType, item, i.CustomFields, isCreate: true, ct);
        items.Add(item);
        await items.SaveChangesAsync(ct);
        return ItemDto.From(item, await fields.ReadAsync(ItemRules.EntityType, item, ct));
    }
}
