using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Reverses a payout: its journal, and on the bill route its bill too (refused once the bill has been paid). The period can then be paid again.</summary>
public sealed record VoidOwnerPayoutCommand(Guid Id, string? Reason) : ICommand<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsManage; }
