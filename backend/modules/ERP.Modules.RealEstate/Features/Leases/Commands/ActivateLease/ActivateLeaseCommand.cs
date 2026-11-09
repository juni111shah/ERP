using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Activates a draft lease: the unit becomes occupied and the rent schedule is generated.</summary>
public sealed record ActivateLeaseCommand(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesActivate;
}
