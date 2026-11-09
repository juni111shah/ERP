using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Creates a bill of materials with its components, priced from the catalog and current stock costs.</summary>
public record CreateBomCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    bool IsActive,
    string? Description,
    List<BomComponentInput>? Components,
    string? CustomFields) : ICommand<BillOfMaterialsDto>;
