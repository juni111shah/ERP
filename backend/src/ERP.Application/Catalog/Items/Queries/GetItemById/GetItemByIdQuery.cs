using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Catalog;

/// <summary>One item with its custom fields.</summary>
public sealed record GetItemByIdQuery(Guid Id) : IQuery<ItemDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ItemsRead;
}
