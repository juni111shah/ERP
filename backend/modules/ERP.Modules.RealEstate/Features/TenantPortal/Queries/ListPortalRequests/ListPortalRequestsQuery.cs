using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The signed-in tenant's maintenance requests, latest 100.</summary>
public sealed record ListPortalRequestsQuery : IQuery<IReadOnlyList<PortalRequestDto>>;
