using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a deal until it is invoiced or lost; it stays in its legal entity.</summary>
public sealed record UpdateDealCommand(Guid Id, DealInput Input) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsEdit;
}
