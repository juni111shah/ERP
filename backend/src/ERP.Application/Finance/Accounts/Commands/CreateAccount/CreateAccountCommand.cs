using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Adds an account to the chart; its code is unique and follows the numbering mask.</summary>
public sealed record CreateAccountCommand(AccountInput Input) : ICommand<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsWrite;
}
