using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Cancels a draft lease.</summary>
public sealed record CancelLeaseCommand(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}
