using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The signed-in tenant's leases with the rent schedule (state: scheduled, invoiced, paid, overdue).</summary>
public sealed record GetPortalLeasesQuery : IQuery<IReadOnlyList<PortalLeaseDto>>;
