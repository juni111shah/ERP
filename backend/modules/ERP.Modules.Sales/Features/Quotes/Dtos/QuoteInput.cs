using System.Text.Json.Nodes;

namespace ERP.Modules.Sales.Features;

public sealed record QuoteLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record QuoteInput(
    Guid LegalEntityId,
    string? Number,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    string? Notes,
    string? Terms,
    List<QuoteLineInput> Lines,
    JsonObject? CustomFields = null);
