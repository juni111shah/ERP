using System.Text.Json.Nodes;

namespace ERP.Modules.Purchasing.Features;

public sealed record GoodsReceiptLineInput(
    Guid PurchaseOrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal QuantityReceived,
    string UnitOfMeasure);

public sealed record GoodsReceiptInput(
    Guid LegalEntityId,
    string? Number,
    Guid PurchaseOrderId,
    string ReceivedBy,
    DateOnly ReceiptDate,
    string? DeliveryNoteNumber,
    string? Notes,
    List<GoodsReceiptLineInput> Lines,
    JsonObject? CustomFields = null,
    Guid? WarehouseId = null);
