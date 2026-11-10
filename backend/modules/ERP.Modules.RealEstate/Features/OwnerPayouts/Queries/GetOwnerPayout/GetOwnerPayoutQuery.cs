using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One owner payout.</summary>
public sealed record GetOwnerPayoutQuery(Guid Id) : IQuery<OwnerPayoutDto>, IRequirePermission { public string RequiredPermission => P.OwnerPayoutsRead; }
