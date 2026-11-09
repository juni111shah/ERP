using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Raises the work orders that became due (the nightly job does the same).</summary>
public sealed record RunPreventiveMaintenanceCommand : ICommand<PmRunResultDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }
