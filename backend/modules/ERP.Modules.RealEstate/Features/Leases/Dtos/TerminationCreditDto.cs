namespace ERP.Modules.RealEstate.Features;

public sealed record TerminationCreditDto(Guid InstalmentId, Guid CreditNoteId, string Number, decimal Amount, bool AppliedToInvoice);
