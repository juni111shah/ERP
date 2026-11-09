using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Pays the owner what the statement of a period shows for the rent held in the owner payable, clearing it: the fee and the owner's maintenance leave the payable at the same time. One live payout per owner, entity and period.</summary>
public sealed record CreateOwnerPayoutCommand(OwnerPayoutInput Input) : ICommand<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsManage; }
