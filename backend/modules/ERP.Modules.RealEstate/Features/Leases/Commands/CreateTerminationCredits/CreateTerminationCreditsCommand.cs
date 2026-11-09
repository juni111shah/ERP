using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>For a terminated lease: credits rent that was invoiced beyond the termination date (whole instalments after it, the unused days of the one it falls in). Safe to run again.</summary>
public sealed record CreateTerminationCreditsCommand(Guid LeaseId) : ICommand<IReadOnlyList<TerminationCreditDto>>, IRequirePermission { public string RequiredPermission => P.LeasesTerminate; }
