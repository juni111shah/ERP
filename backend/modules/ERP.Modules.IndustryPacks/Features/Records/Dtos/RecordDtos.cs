using System.Text.Json.Nodes;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

public sealed record RecordLineInput(string Description, decimal Quantity, decimal UnitPrice, string? Code = null, Guid? ItemId = null, string? UnitOfMeasure = null, DateOnly? DueDate = null, decimal? CompletedQuantity = null, Guid? Id = null);

public sealed record RecordInput(
    Guid LegalEntityId, string Title, DateOnly RecordDate, JsonObject? Data = null, Guid? PartyId = null, Guid? ParentId = null, Guid? BusinessUnitId = null,
    DateOnly? DueDate = null, decimal? Amount = null, string? Notes = null, List<RecordLineInput>? Lines = null);

public sealed record RecordLineDto(Guid Id, int Seq, string? Code, string Description, Guid? ItemId, string? UnitOfMeasure, decimal Quantity, decimal UnitPrice, decimal Amount, decimal CompletedQuantity, DateOnly? DueDate, Guid? InvoiceId);
public sealed record RecordEventDto(Guid Id, string? FromStatus, string ToStatus, DateTime OccurredAt, string? UserName, string? Reason);

public sealed record RecordDto(
    Guid Id, string ModuleKey, string RecordType, Guid LegalEntityId, string Number, string Title, string Status, Guid? PartyId, string? PartyName, Guid? ParentId, string? ParentTitle,
    Guid? BusinessUnitId, DateOnly RecordDate, DateOnly? DueDate, decimal Amount, JsonObject Data, string? Notes, Guid? InvoiceId, Guid? BillId, DateOnly? ConsumedOn, DateTime CreatedAt,
    IReadOnlyList<RecordLineDto> Lines, IReadOnlyList<RecordEventDto> Events, IReadOnlyList<RecordTransitionDef> NextSteps);

public sealed record RecordProgressLine(Guid LineId, decimal CompletedQuantity);
