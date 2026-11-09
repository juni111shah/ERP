using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The next free code under a parent (or the next root of a type). In free mode it returns an empty code: nothing is suggested.</summary>
public sealed record GetNextAccountCodeQuery(Guid? ParentId, string? Type) : IQuery<NextAccountCodeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsWrite;
}
