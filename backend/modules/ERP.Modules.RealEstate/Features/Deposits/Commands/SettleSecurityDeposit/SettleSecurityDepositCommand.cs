using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>On move-out: damages become income, arrears settle the tenant's open invoices, the rest is refunded from a bank account.</summary>
public sealed record SettleSecurityDepositCommand(Guid LeaseId, SettleDepositInput Input) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }
