using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Explodes the open work orders through their BOMs and nets each item against stock. ItemId with Quantity adds a what-if demand for that many of an item through its active BOM, on top of the open orders.</summary>
public record GetMaterialRequirementsQuery(Guid LegalEntityId, Guid? WarehouseId = null, Guid? ItemId = null, decimal? Quantity = null) : IQuery<MaterialRequirementsDto>;
