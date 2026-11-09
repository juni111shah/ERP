using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The tenant pays the security deposit: Dr bank, Cr deposits held.</summary>
public sealed record RecordSecurityDepositCommand(Guid LeaseId, Guid BankAccountId, DateOnly Date, decimal Amount) : ICommand<DepositDto>, IRequirePermission { public string RequiredPermission => P.DepositsManage; }
