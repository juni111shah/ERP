using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates draft renewals for the selected leases, raising the rent by UpliftPercent (0 = same rent).</summary>
public sealed record CreateRenewalDraftsCommand(IReadOnlyList<Guid> LeaseIds, decimal UpliftPercent) : ICommand<IReadOnlyList<RenewalDraftResult>>, IRequirePermission { public string RequiredPermission => P.LeasesRenew; }
