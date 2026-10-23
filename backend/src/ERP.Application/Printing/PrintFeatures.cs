using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed record PrintReceiptDto(
    PrintHeaderDto Header,
    ReceiptDto Receipt,
    string PartyName,
    string? PartyAddress,
    IReadOnlyList<AllocationDto> Allocations);

public sealed record PrintBillDto(
    PrintHeaderDto Header,
    BillDto Bill,
    string PartyName,
    string? PartyAddress,
    IReadOnlyList<BillLineDto> Lines);

public sealed record PrintPaymentDto(
    PrintHeaderDto Header,
    PaymentDto Payment,
    string PartyName,
    IReadOnlyList<BillAllocationDto> Allocations);

public sealed record PrintJournalDto(
    PrintHeaderDto Header,
    JournalDto Journal,
    IReadOnlyList<JournalLineDto> Lines);

/// <summary>Get print data for an invoice or credit note.</summary>
public sealed record GetPrintInvoice(Guid Id) : IQuery<PrintInvoiceDto>, IRequirePermission 
{ 
    public string RequiredPermission => CorePermissions.InvoicesRead; 
}

/// <summary>Get print data for a receipt.</summary>
public sealed record GetPrintReceipt(Guid Id) : IQuery<PrintReceiptDto>, IRequirePermission 
{ 
    public string RequiredPermission => CorePermissions.ReceiptsRead; 
}

/// <summary>Get print data for a bill.</summary>
public sealed record GetPrintBill(Guid Id) : IQuery<PrintBillDto>, IRequirePermission 
{ 
    public string RequiredPermission => CorePermissions.BillsRead; 
}

/// <summary>Get print data for a payment.</summary>
public sealed record GetPrintPayment(Guid Id) : IQuery<PrintPaymentDto>, IRequirePermission 
{ 
    public string RequiredPermission => CorePermissions.PaymentsRead; 
}

/// <summary>Get print data for a journal entry.</summary>
public sealed record GetPrintJournal(Guid Id) : IQuery<PrintJournalDto>, IRequirePermission 
{ 
    public string RequiredPermission => CorePermissions.JournalsRead; 
}

internal sealed class PrintHandlers(
    IRepository<Invoice> invoices,
    IRepository<Settlement> receipts,
    IRepository<Bill> bills,
    IRepository<Settlement> payments,
    IRepository<JournalEntry> journals,
    IReadRepository<Account> accounts,
    IRepository<LegalEntity> entities,
    IReadRepository<Domain.Parties.Party> parties,
    IReadRepository<Organization> orgs,
    ICurrentUser user) :
    IRequestHandler<GetPrintInvoice, PrintInvoiceDto>,
    IRequestHandler<GetPrintReceipt, PrintReceiptDto>,
    IRequestHandler<GetPrintBill, PrintBillDto>,
    IRequestHandler<GetPrintPayment, PrintPaymentDto>,
    IRequestHandler<GetPrintJournal, PrintJournalDto>
{
    public async Task<PrintInvoiceDto> Handle(GetPrintInvoice q, CancellationToken ct)
    {
        var invoice = await invoices.Query(new Specification<Invoice>(i => i.Id == q.Id).Include(i => i.Lines))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var header = await BuildHeaderAsync(invoice.LegalEntityId, ct);
        var party = await parties.Query().FirstOrDefaultAsync(p => p.Id == invoice.PartyId, ct);
        var partyAddress = party?.Address ?? string.Empty;
        
        return new PrintInvoiceDto(
            header,
            InvoiceDto.From(invoice),
            partyAddress,
            invoice.Lines.OrderBy(l => l.Seq).Select(l => new InvoiceLineDto(
                l.Id, l.Seq, l.ItemId, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.Net, l.AccountId)).ToList());
    }

    public async Task<PrintReceiptDto> Handle(GetPrintReceipt q, CancellationToken ct)
    {
        var receipt = await receipts.Query(new Specification<Settlement>(r => r.Id == q.Id && r.Direction == InstrumentDirection.Received).Include(r => r.Allocations))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var header = await BuildHeaderAsync(receipt.LegalEntityId, ct);
        var party = await parties.Query().FirstOrDefaultAsync(p => p.Id == receipt.PartyId, ct);
        
        var ids = receipt.Allocations.Select(a => a.InvoiceId!.Value).ToList();
        var invoiceNumbers = await invoices.Query(new Specification<Invoice>(i => ids.Contains(i.Id)))
            .Select(i => new { i.Id, i.Number }).ToDictionaryAsync(i => i.Id, i => i.Number, ct);
        var allocations = receipt.Allocations.Select(a => new AllocationDto(
            a.Id, a.InvoiceId!.Value, invoiceNumbers.GetValueOrDefault(a.InvoiceId.Value, "?"), a.Amount)).ToList();
        
        var receiptDto = new ReceiptDto(
            receipt.Id, receipt.Number, receipt.LegalEntityId, receipt.PartyId, receipt.PartyName, receipt.Date, 
            receipt.Method, receipt.BankAccountId, receipt.CurrencyCode, receipt.Amount, receipt.Unallocated, 
            receipt.Reference, receipt.Status, receipt.JournalEntryId, allocations);
        
        return new PrintReceiptDto(
            header,
            receiptDto,
            party?.Name ?? receipt.PartyName ?? "Unknown",
            party?.Address,
            allocations);
    }

    public async Task<PrintBillDto> Handle(GetPrintBill q, CancellationToken ct)
    {
        var bill = await bills.Query(new Specification<Bill>(b => b.Id == q.Id).Include(b => b.Lines))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var header = await BuildHeaderAsync(bill.LegalEntityId, ct);
        var party = await parties.Query().FirstOrDefaultAsync(p => p.Id == bill.PartyId, ct);
        
        var lines = bill.Lines.OrderBy(l => l.Seq).Select(l => new BillLineDto(
            l.Id, l.Seq, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.TaxRecoverable, l.Net, l.AccountId)).ToList();
        
        var billDto = new BillDto(
            bill.Id, bill.Number, bill.LegalEntityId, bill.PartyId, bill.PartyName, bill.VendorReference, 
            bill.BillDate, bill.DueDate, bill.CurrencyCode, bill.Net, bill.Tax, bill.Total, bill.Paid, 
            bill.Status, bill.ApprovalRequestId, bill.JournalEntryId, bill.Notes, lines);
        
        return new PrintBillDto(
            header,
            billDto,
            party?.Name ?? bill.PartyName ?? "Unknown",
            party?.Address,
            lines);
    }

    public async Task<PrintPaymentDto> Handle(GetPrintPayment q, CancellationToken ct)
    {
        var payment = await payments.Query(new Specification<Settlement>(p => p.Id == q.Id && p.Direction == InstrumentDirection.Issued).Include(p => p.Allocations))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var header = await BuildHeaderAsync(payment.LegalEntityId, ct);
        var party = await parties.Query().FirstOrDefaultAsync(p => p.Id == payment.PartyId, ct);
        
        var ids = payment.Allocations.Select(a => a.BillId!.Value).ToList();
        var billNumbers = await bills.Query(new Specification<Bill>(b => ids.Contains(b.Id)))
            .Select(b => new { b.Id, b.Number }).ToDictionaryAsync(b => b.Id, b => b.Number, ct);
        var allocations = payment.Allocations.Select(a => new BillAllocationDto(
            a.Id, a.BillId!.Value, billNumbers.GetValueOrDefault(a.BillId.Value, "?"), a.Amount)).ToList();
        
        var paymentDto = new PaymentDto(
            payment.Id, payment.Number, payment.LegalEntityId, payment.PartyId, payment.PartyName, payment.Date,
            payment.Method, payment.BankAccountId, payment.CurrencyCode, payment.Amount, payment.Unallocated,
            payment.Reference, payment.Status, payment.PaymentInstrumentId, payment.JournalEntryId, allocations);
        
        return new PrintPaymentDto(
            header,
            paymentDto,
            party?.Name ?? payment.PartyName ?? "Unknown",
            allocations);
    }

    public async Task<PrintJournalDto> Handle(GetPrintJournal q, CancellationToken ct)
    {
        var journal = await journals.Query(new Specification<JournalEntry>(j => j.Id == q.Id).Include(j => j.Lines))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var header = await BuildHeaderAsync(journal.LegalEntityId, ct);
        
        var accountIds = journal.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accountDict = await accounts.Query(new Specification<Account>(a => accountIds.Contains(a.Id)))
            .Select(a => new { a.Id, a.Code, a.Name }).ToDictionaryAsync(a => a.Id, ct);
        var lines = journal.Lines.OrderBy(l => l.Seq).Select(l => new JournalLineDto(
            l.Id, l.Seq, l.AccountId, accountDict[l.AccountId].Code, accountDict[l.AccountId].Name, l.Debit, l.Credit, l.Description, l.PartyId)).ToList();
        
        var journalDto = new JournalDto(
            journal.Id, journal.Number, journal.LegalEntityId, journal.Date, journal.Memo, journal.Reference,
            journal.SourceType, journal.SourceId, journal.CurrencyCode, journal.Status, journal.Amount,
            journal.ReversalOfId, journal.PostedAt, lines);
        
        return new PrintJournalDto(
            header,
            journalDto,
            lines);
    }

    private async Task<PrintHeaderDto> BuildHeaderAsync(Guid legalEntityId, CancellationToken ct)
    {
        var entity = await entities.Query().FirstOrDefaultAsync(e => e.Id == legalEntityId, ct) 
            ?? throw new NotFoundException("Legal entity not found");
        var org = await orgs.Query().FirstOrDefaultAsync(o => o.Id == (user.OrgId ?? Guid.Empty), ct)
            ?? throw new NotFoundException("Organization not found");

        return new PrintHeaderDto(
            org.Name,
            entity.Name,
            entity.Address,
            entity.TaxRegistrationNo,
            entity.RegistrationNo,
            entity.Phone,
            entity.Email,
            entity.BaseCurrency,
            entity.CountryCode);
    }
}
