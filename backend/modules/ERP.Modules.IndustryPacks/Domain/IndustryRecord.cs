using ERP.Domain.Common;

namespace ERP.Modules.IndustryPacks.Domain;

/// <summary>
/// One business record of an industry module, such as a patient, a shipment or a bill of quantities. What it means is decided by its
/// <see cref="RecordTypeDef"/>: fields live in <see cref="Data"/>, and the definition validates them.
/// </summary>
[DefaultSort("-recordDate")]
public class IndustryRecord : LegalEntityScopedEntity, IHasCustomFields
{
    [Filterable]
    public string ModuleKey { get; set; } = default!;
    [Filterable]
    public string RecordType { get; set; } = default!;
    [Listable]
    public string Number { get; set; } = default!;
    [Listable]
    public string Title { get; set; } = default!;
    [Filterable, Sortable]
    public string Status { get; set; } = default!;
    [Filterable]
    public Guid? PartyId { get; set; }
    [Searchable, Sortable]
    public string? PartyName { get; set; }
    /// <summary>The record this one belongs to, such as the patient of an appointment. Must be of the type its definition names.</summary>
    [Filterable]
    public Guid? ParentId { get; set; }
    [Searchable]
    public string? ParentTitle { get; set; }
    [Filterable]
    public Guid? BusinessUnitId { get; set; }
    [Filterable, Sortable]
    public DateOnly RecordDate { get; set; }
    [Filterable, Sortable]
    public DateOnly? DueDate { get; set; }
    [Filterable, Sortable]
    public decimal Amount { get; set; }
    /// <summary>JSON object of the values of the definition's fields.</summary>
    public string Data { get; set; } = "{}";
    public string? Notes { get; set; }
    [Filterable]
    public Guid? InvoiceId { get; set; }
    [Filterable]
    public Guid? BillId { get; set; }
    /// <summary>Set once the lines' materials left stock; the stock reference is this record's number.</summary>
    public DateOnly? ConsumedOn { get; set; }
    public string? CustomFields { get; set; }

    public List<IndustryRecordLine> Lines { get; set; } = [];
    public List<IndustryRecordEvent> Events { get; set; } = [];
}

/// <summary>A priced line: a BOQ item, a fee, an instalment or a material. <see cref="InvoiceId"/> is set when the line was billed on its own.</summary>
public class IndustryRecordLine : OrgEntity
{
    public Guid RecordId { get; set; }
    public int Seq { get; set; }
    public string? Code { get; set; }
    public string Description { get; set; } = default!;
    public Guid? ItemId { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Cumulative quantity certified or done, for records that track progress.</summary>
    public decimal CompletedQuantity { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? InvoiceId { get; set; }
}

/// <summary>Append-only status history: who moved the record, when and why.</summary>
public class IndustryRecordEvent : OrgEntity
{
    public Guid RecordId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = default!;
    public DateTime OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Reason { get; set; }
}
