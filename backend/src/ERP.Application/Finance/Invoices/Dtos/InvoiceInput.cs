using System.Text.Json.Serialization;

namespace ERP.Application.Finance;

/// <summary>One line of an invoice or credit note as the client sends it.</summary>
public sealed record InvoiceLineInput(string Description, decimal Quantity, decimal Price, Guid? TaxCodeId = null, Guid? AccountId = null, Guid? ItemId = null);

/// <summary>
/// The body of create / update invoice. <see cref="SourceType"/> and <see cref="SourceId"/> are set only by module billing code
/// (a sales order, a milestone...); they are never read from a request body.
/// </summary>
public sealed record InvoiceInput(
    Guid LegalEntityId,
    Guid PartyId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    List<InvoiceLineInput> Lines,
    bool IsCreditNote = false,
    string? Notes = null,
    [property: JsonIgnore] string? SourceType = null,
    [property: JsonIgnore] Guid? SourceId = null,
    Guid? BusinessUnitId = null)
{
    /// <summary>The draft the invoicing service builds the document from (descriptions trimmed).</summary>
    public InvoiceDraft ToDraft() => new(
        LegalEntityId, PartyId, InvoiceDate, DueDate,
        Lines.Select(l => new InvoiceLineDraft(l.Description.Trim(), l.Quantity, l.Price, l.TaxCodeId, l.AccountId, l.ItemId)).ToList(),
        IsCreditNote, SourceType: SourceType, SourceId: SourceId, BusinessUnitId: BusinessUnitId, Notes: Notes);
}

/// <summary>One line to credit: an original invoice line and how much of its quantity to credit.</summary>
public sealed record CreditNoteLineInput(Guid InvoiceLineId, decimal Quantity);
