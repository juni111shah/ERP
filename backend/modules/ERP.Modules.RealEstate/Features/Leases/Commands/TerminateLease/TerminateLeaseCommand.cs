using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Terminates an active lease early: the schedule is cut at the date and the unit is vacated.</summary>
public sealed record TerminateLeaseCommand(Guid Id, DateOnly Date, string Reason) : ICommand<TerminationResultDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesTerminate;
}
