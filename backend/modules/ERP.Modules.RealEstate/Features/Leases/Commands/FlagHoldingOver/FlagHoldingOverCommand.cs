using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Marks that the tenant stays after the end date (the nightly job then holds the lease over instead of ending it).</summary>
public sealed record FlagHoldingOverCommand(Guid Id, bool HoldingOver) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesEdit;
}
