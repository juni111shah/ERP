using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Updates a bill of materials; components, when given, replace the current ones.</summary>
public record UpdateBomCommand(
    Guid Id,
    string Name,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    bool IsActive,
    string? Description,
    List<BomComponentInput>? Components,
    string? CustomFields) : ICommand<BillOfMaterialsDto>;
