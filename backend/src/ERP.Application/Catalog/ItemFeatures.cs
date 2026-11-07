using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace ERP.Application.Catalog;

public sealed record ItemInput(
    string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId,
    bool IsActive = true, JsonObject? CustomFields = null, string? Barcode = null, string? Category = null);

public sealed record ItemDto(
    Guid Id, string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId,
    bool IsActive, JsonObject CustomFields, string? Barcode = null, string? Category = null)
{
    public static ItemDto From(Item i, JsonObject customFields) => new(i.Id, i.Sku, i.Name, i.Type, i.Uom, i.SalesPrice, i.PurchasePrice, i.TaxCodeId, i.IsActive, customFields, i.Barcode, i.Category);
}

public sealed record CreateItem(ItemInput Input) : ICommand<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsWrite;
}

public sealed record GetItem(Guid Id) : IQuery<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsRead;
}

public sealed record UpdateItem(Guid Id, ItemInput Input) : ICommand<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsWrite;
}

public sealed class UpdateItemValidator : AbstractValidator<UpdateItem>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.Input.Sku).NotEmpty().MaximumLength(50).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Barcode).MaximumLength(64);
    }
}

public sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Input.Sku).NotEmpty().MaximumLength(50).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Barcode).MaximumLength(64);
    }
}

internal sealed class ItemHandlers(IRepository<Item> items, ICustomFieldService fields) :
    IRequestHandler<GetItem, ItemDto>,
    IRequestHandler<CreateItem, ItemDto>,
    IRequestHandler<UpdateItem, ItemDto>
{
    private const string EntityType = "item";

    public async Task<ItemDto> Handle(GetItem q, CancellationToken ct)
    {
        var item = await items.GetAsync(q.Id, ct);
        return ItemDto.From(item, await fields.ReadAsync(EntityType, item, ct));
    }

    public async Task<ItemDto> Handle(UpdateItem cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var item = await items.GetForUpdateAsync(cmd.Id, ct);
        var sku = i.Sku.Trim();
        if (sku != item.Sku && await items.AnyAsync(x => x.Sku == sku, ct)) throw new ConflictException("An item with that SKU exists.");
        item.Sku = sku; item.Name = i.Name.Trim(); item.Type = i.Type; item.Uom = i.Uom; item.SalesPrice = i.SalesPrice;
        item.PurchasePrice = i.PurchasePrice; item.TaxCodeId = i.TaxCodeId; item.IsActive = i.IsActive;
        // Null leaves it alone, like the barcode.
        if (i.Category is not null) item.Category = i.Category.Trim() is { Length: > 0 } category ? category : null;
        // Null leaves it alone (callers that do not know about barcodes); an empty string clears it.
        if (i.Barcode is not null) item.Barcode = await CheckedBarcodeAsync(i.Barcode, item.Id, ct);
        await fields.ApplyAsync(EntityType, item, i.CustomFields, isCreate: false, ct);
        await items.SaveChangesAsync(ct);
        return ItemDto.From(item, await fields.ReadAsync(EntityType, item, ct));
    }

    public async Task<ItemDto> Handle(CreateItem cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var sku = i.Sku.Trim();
        if (await items.AnyAsync(x => x.Sku == sku, ct)) throw new ConflictException("An item with that SKU exists.");
        var item = new Item { Sku = sku, Name = i.Name.Trim(), Type = i.Type, Uom = i.Uom, SalesPrice = i.SalesPrice, PurchasePrice = i.PurchasePrice, TaxCodeId = i.TaxCodeId, IsActive = i.IsActive, Category = string.IsNullOrWhiteSpace(i.Category) ? null : i.Category.Trim() };
        item.Barcode = await CheckedBarcodeAsync(i.Barcode, item.Id, ct);
        await fields.ApplyAsync(EntityType, item, i.CustomFields, isCreate: true, ct);
        items.Add(item);
        await items.SaveChangesAsync(ct);
        return ItemDto.From(item, await fields.ReadAsync(EntityType, item, ct));
    }

    private async Task<string?> CheckedBarcodeAsync(string? barcode, Guid itemId, CancellationToken ct)
    {
        var code = barcode?.Trim();
        if (string.IsNullOrEmpty(code)) return null;
        if (await items.AnyAsync(x => x.Barcode == code && x.Id != itemId, ct)) throw new ConflictException("Another item already has that barcode.");
        return code;
    }
}
