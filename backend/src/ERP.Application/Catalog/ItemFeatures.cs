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
    bool IsActive = true, JsonObject? CustomFields = null);

public sealed record ItemDto(
    Guid Id, string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId,
    bool IsActive, JsonObject CustomFields)
{
    public static ItemDto From(Item i, JsonObject customFields) => new(i.Id, i.Sku, i.Name, i.Type, i.Uom, i.SalesPrice, i.PurchasePrice, i.TaxCodeId, i.IsActive, customFields);
}

public sealed record CreateItem(ItemInput Input) : ICommand<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsWrite;
}

public sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Input.Sku).NotEmpty().MaximumLength(50).WithMessage("SKU and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("SKU and name are required.");
    }
}

internal sealed class ItemHandlers(IRepository<Item> items, ICustomFieldService fields) : IRequestHandler<CreateItem, ItemDto>
{
    private const string EntityType = "item";

    public async Task<ItemDto> Handle(CreateItem cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var sku = i.Sku.Trim();
        if (await items.AnyAsync(x => x.Sku == sku, ct)) throw new ConflictException("An item with that SKU exists.");
        var item = new Item { Sku = sku, Name = i.Name.Trim(), Type = i.Type, Uom = i.Uom, SalesPrice = i.SalesPrice, PurchasePrice = i.PurchasePrice, TaxCodeId = i.TaxCodeId, IsActive = i.IsActive };
        await fields.ApplyAsync(EntityType, item, i.CustomFields, isCreate: true, ct);
        items.Add(item);
        await items.SaveChangesAsync(ct);
        return ItemDto.From(item, await fields.ReadAsync(EntityType, item, ct));
    }
}
