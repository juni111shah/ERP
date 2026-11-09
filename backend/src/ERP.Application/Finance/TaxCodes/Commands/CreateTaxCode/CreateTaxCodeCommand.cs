using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Adds a workspace tax code; codes are upper case and unique.</summary>
public sealed record CreateTaxCodeCommand(TaxCodeInput Input) : ICommand<TaxCodeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.TaxWrite;
}
