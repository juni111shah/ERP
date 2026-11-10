using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>A delivery route with its stops; null when it does not exist.</summary>
public record GetRouteByIdQuery(Guid Id) : IQuery<DistributionRouteDto?>;
