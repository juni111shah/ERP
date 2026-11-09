using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Changes a tax code; a new code must still be unique within its scope.</summary>
public sealed record UpdateTaxCodeCommand(Guid Id, TaxCodeInput Input) : ICommand<TaxCodeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.TaxWrite;
}
