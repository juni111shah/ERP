namespace ERP.Modules.Manufacturing.Features;

/// <summary>
/// One item's position. GrossRequired is what open orders (and any what-if quantity) need; NetRequired is what is left after on-hand, on-order and in-production,
/// and is to be made when IsManufactured, otherwise bought. Level 0 is what is being produced; deeper levels are its components.
/// </summary>
public record MaterialRequirementDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    int Level,
    bool IsManufactured,
    decimal GrossRequired,
    decimal OnHand,
    decimal OnOrder,
    decimal InProduction,
    decimal NetRequired);

/// <summary>OpenPurchaseNetted is false until something exposes open purchase quantities to this module: OnOrder is then zero and shortages may be overstated.</summary>
public record MaterialRequirementsDto(
    Guid LegalEntityId,
    Guid? WarehouseId,
    int OpenWorkOrders,
    bool OpenPurchaseNetted,
    List<MaterialRequirementDto> Rows,
    List<MaterialRequirementDto> Shortages);
