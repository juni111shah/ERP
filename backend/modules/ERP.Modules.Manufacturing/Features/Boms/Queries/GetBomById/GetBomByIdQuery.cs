using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>A bill of materials with its components at current cost; null when it does not exist.</summary>
public record GetBomByIdQuery(Guid Id) : IQuery<BillOfMaterialsDto?>;
