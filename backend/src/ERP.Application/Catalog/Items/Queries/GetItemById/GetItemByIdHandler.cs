using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Catalog;

namespace ERP.Application.Catalog;

internal sealed class GetItemByIdHandler(IReadRepository<Item> items, ICustomFieldService fields) : IQueryHandler<GetItemByIdQuery, ItemDto>
{
    public async Task<ItemDto> Handle(GetItemByIdQuery query, CancellationToken ct)
    {
        var item = await items.GetAsync(query.Id, ct);
        return ItemDto.From(item, await fields.ReadAsync(ItemRules.EntityType, item, ct));
    }
}
