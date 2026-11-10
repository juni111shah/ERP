using System.Text.Json.Nodes;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseOrderLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record PurchaseOrderInput(
    Guid LegalEntityId,
    string? Number,
    Guid? RequisitionId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorContact,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Notes,
    List<PurchaseOrderLineInput> Lines,
    JsonObject? CustomFields = null);

/// <summary>How much of an order line to put on this bill.</summary>
public sealed record BillOrderLineInput(Guid LineId, decimal Quantity);
