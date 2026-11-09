using System.Text.Json.Nodes;

namespace ERP.Modules.Purchasing.Features;

public sealed record RequisitionLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal EstimatedUnitPrice);

public sealed record RequisitionInput(
    Guid LegalEntityId,
    string? Number,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    string? Reason,
    List<RequisitionLineInput> Lines,
    JsonObject? CustomFields = null);
