using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>System accounts (those posting rules rely on) keep their code, type and active state; only the descriptive fields change.</summary>
public sealed record UpdateAccountCommand(Guid Id, AccountInput Input) : ICommand<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsWrite;
}
