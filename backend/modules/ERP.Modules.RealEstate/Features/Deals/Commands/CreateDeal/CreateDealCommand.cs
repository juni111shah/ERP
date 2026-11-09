using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a brokerage deal in a legal entity the caller may use, with its commission worked out.</summary>
public sealed record CreateDealCommand(DealInput Input) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsCreate;
}
