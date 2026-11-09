using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Work orders assigned to the signed-in vendor.</summary>
public sealed record ListVendorWorkOrdersQuery : IQuery<IReadOnlyList<VendorWorkOrderDto>>;
