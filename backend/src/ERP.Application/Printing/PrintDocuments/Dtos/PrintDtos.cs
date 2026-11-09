using ERP.Application.Finance;
using ERP.Domain.Finance;

namespace ERP.Application.Printing;

public sealed record PrintHeaderDto(
    string OrganizationName,
    string LegalEntityName,
    string? LegalEntityAddress,
    string? TaxRegistrationNo,
    string? RegistrationNo,
    string? Phone,
    string? Email,
    string BaseCurrency,
    string CountryCode);

public sealed record PrintInvoiceDto(
    PrintHeaderDto Header,
    InvoiceDto Invoice,
    string PartyAddress,
    IReadOnlyList<InvoiceLineDto> Lines, string? PartyTaxNumber = null, string? PartyPhone = null, string? PartyEmail = null,
    Guid? OriginalInvoiceId = null, string? OriginalInvoiceNumber = null);

public sealed record PrintReceiptDto(
    PrintHeaderDto Header,
    ReceiptDto Receipt,
    string PartyName,
    string? PartyAddress,
    IReadOnlyList<AllocationDto> Allocations, string? PartyTaxNumber = null, string? PartyPhone = null, string? PartyEmail = null,
    string? InstrumentKind = null, string? ChequeNumber = null, DateOnly? ChequeDate = null, string? ChequeStatus = null, string? ChequeBankName = null);

public sealed record PrintBillDto(
    PrintHeaderDto Header,
    BillDto Bill,
    string PartyName,
    string? PartyAddress,
    IReadOnlyList<BillLineDto> Lines, string? PartyTaxNumber = null, string? PartyPhone = null, string? PartyEmail = null);

public sealed record PrintPaymentDto(
    PrintHeaderDto Header,
    PaymentDto Payment,
    string PartyName,
    IReadOnlyList<BillAllocationDto> Allocations, string? PartyAddress = null, string? PartyTaxNumber = null, string? PartyPhone = null, string? PartyEmail = null,
    string? InstrumentKind = null, string? ChequeNumber = null, DateOnly? ChequeDate = null, string? ChequeStatus = null, string? ChequeBankName = null);

public sealed record PrintJournalDto(
    PrintHeaderDto Header,
    JournalDto Journal,
    IReadOnlyList<JournalLineDto> Lines, JournalStatus Status = JournalStatus.Draft);

/// <summary>Cheque / instrument details shown on receipt and payment prints (null when the settlement has no instrument).</summary>
internal sealed record PrintInstrument(string Kind, string No, DateOnly Date, string Status, string? BankName);
