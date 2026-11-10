using System.Text.Json.Nodes;

namespace ERP.Modules.Sales.Features;

public sealed record OrderLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record OrderInput(
    Guid LegalEntityId,
    string? Number,
    Guid? QuoteId,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    string? PaymentTerms,
    string? ShippingAddress,
    string? Notes,
    List<OrderLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record OrderFulfillmentLineInput(Guid LineId, decimal DeliveredQuantity);
