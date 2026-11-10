using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Security;

internal sealed class ListPermissionsHandler(IPermissionCatalog catalog) : IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionDefinition>>
{
    public Task<IReadOnlyList<PermissionDefinition>> Handle(ListPermissionsQuery query, CancellationToken ct) => Task.FromResult(catalog.All);
}
