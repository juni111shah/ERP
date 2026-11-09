using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>A van sales trip with what was loaded; null when it does not exist.</summary>
public record GetTripByIdQuery(Guid Id) : IQuery<VanSalesTripDto?>;
