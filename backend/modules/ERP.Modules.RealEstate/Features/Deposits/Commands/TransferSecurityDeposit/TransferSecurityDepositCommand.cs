using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Rolls the held deposit over to the renewal lease without any cash moving.</summary>
public sealed record TransferSecurityDepositCommand(Guid FromLeaseId, Guid ToLeaseId) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }
