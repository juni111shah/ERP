using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One account with its custom fields (the step-by-step screen opens the edit drawer from a panel row).</summary>
public sealed record GetAccountQuery(Guid Id) : IQuery<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsRead;
}
