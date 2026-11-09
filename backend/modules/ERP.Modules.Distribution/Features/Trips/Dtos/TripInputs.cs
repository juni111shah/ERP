namespace ERP.Modules.Distribution.Features;

public record TripInventoryInput(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    decimal LoadedQuantity,
    decimal UnitPrice);

public record RecordTripItemReconciliationInput(
    Guid ItemId,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal DamagedQuantity);
