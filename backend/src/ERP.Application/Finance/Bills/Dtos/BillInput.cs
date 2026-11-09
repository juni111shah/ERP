namespace ERP.Application.Finance;

/// <summary>One line of a bill as the client sends it.</summary>
public sealed record BillLineInput(string Description, decimal Quantity, decimal Price, Guid AccountId, Guid? TaxCodeId = null, Guid? ItemId = null);

/// <summary>The body of create / update bill.</summary>
public sealed record BillInput(Guid LegalEntityId, Guid PartyId, string? VendorReference, DateOnly BillDate, DateOnly? DueDate, List<BillLineInput> Lines, string? Notes = null, Guid? BusinessUnitId = null);
