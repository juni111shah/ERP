using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>
/// One posted document on a party's account. <see cref="Debit"/> raises what the customer owes (invoice) or lowers what is owed to the
/// vendor (payment); <see cref="Credit"/> does the opposite (credit note, receipt / bill). <see cref="Balance"/> is the running balance after it.
/// </summary>
public sealed record PartyLedgerLine(DateOnly Date, string Kind, Guid DocumentId, string Number, string? Reference, string CurrencyCode, decimal Debit, decimal Credit, decimal Balance);

/// <summary>
/// Statement of account for a date range: opening balance (everything posted before <see cref="From"/>), each document in the range with
/// a running balance, and the closing balance. Customer balances are receivable (positive = the customer owes), vendor balances are
/// payable (positive = owed to the vendor), the same signs as the open-item statements. Draft, pending and voided documents are left out.
/// </summary>
public sealed record PartyLedgerDto(Guid PartyId, string? PartyName, string Side, DateOnly From, DateOnly To, decimal OpeningBalance,
    IReadOnlyList<PartyLedgerLine> Lines, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance);

public sealed record GetCustomerLedger(Guid PartyId, DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<PartyLedgerDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetVendorLedger(Guid PartyId, DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<PartyLedgerDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

public sealed class GetCustomerLedgerValidator : AbstractValidator<GetCustomerLedger>
{
    public GetCustomerLedgerValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= PartyLedger.MaxDays).WithName("To").WithMessage("The date range can be at most 10 years.");
    }
}

public sealed class GetVendorLedgerValidator : AbstractValidator<GetVendorLedger>
{
    public GetVendorLedgerValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= PartyLedger.MaxDays).WithName("To").WithMessage("The date range can be at most 10 years.");
    }
}

/// <summary>Running-balance arithmetic of a party statement (pure; unit-tested).</summary>
public static class PartyLedger
{
    public const int MaxDays = 3660;

    /// <summary>Kinds in the order they sort on the same day: charges first, then settlements.</summary>
    public static int Order(string kind) => kind switch { "invoice" or "bill" => 0, "creditNote" => 1, _ => 2 };

    public static PartyLedgerDto Build(Guid partyId, string? partyName, string side, DateOnly from, DateOnly to, decimal opening, IEnumerable<PartyLedgerLine> unsorted, bool debitRaises)
    {
        var balance = opening;
        var lines = new List<PartyLedgerLine>();
        foreach (var l in unsorted.OrderBy(l => l.Date).ThenBy(l => Order(l.Kind)).ThenBy(l => l.Number, StringComparer.Ordinal))
        {
            balance += debitRaises ? l.Debit - l.Credit : l.Credit - l.Debit;
            lines.Add(l with { Balance = balance });
        }
        return new PartyLedgerDto(partyId, partyName, side, from, to, opening, lines, lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), balance);
    }
}

internal sealed class PartyLedgerHandlers(IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<Settlement> settlements) :
    IRequestHandler<GetCustomerLedger, PartyLedgerDto>,
    IRequestHandler<GetVendorLedger, PartyLedgerDto>
{
    public async Task<PartyLedgerDto> Handle(GetCustomerLedger q, CancellationToken ct)
    {
        var docs = invoices.Query(new Specification<Invoice>(i => i.PartyId == q.PartyId && i.InvoiceDate <= q.To
            && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid || i.Status == DocumentStatus.Paid)
            && (q.LegalEntityId == null || i.LegalEntityId == q.LegalEntityId)));
        var cash = Settled(InstrumentDirection.Received, q.PartyId, q.To, q.LegalEntityId);

        var openingDocs = await docs.Where(i => i.InvoiceDate < q.From).SumAsync(i => (decimal?)(i.IsCreditNote ? -i.Total : i.Total), ct) ?? 0m;
        var openingCash = await cash.Where(s => s.Date < q.From).SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

        var inRange = await docs.Where(i => i.InvoiceDate >= q.From)
            .Select(i => new { i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.PartyName, i.CurrencyCode, i.Total }).ToListAsync(ct);
        var received = await cash.Where(s => s.Date >= q.From)
            .Select(s => new { s.Id, s.Number, s.Date, s.PartyName, s.CurrencyCode, s.Amount, s.Reference }).ToListAsync(ct);

        var lines = inRange.Select(i => new PartyLedgerLine(i.InvoiceDate, i.IsCreditNote ? "creditNote" : "invoice", i.Id, i.Number, null, i.CurrencyCode,
                i.IsCreditNote ? 0 : i.Total, i.IsCreditNote ? i.Total : 0, 0))
            .Concat(received.Select(s => new PartyLedgerLine(s.Date, "receipt", s.Id, s.Number, s.Reference, s.CurrencyCode, 0, s.Amount, 0)));
        var name = inRange.Select(i => i.PartyName).Concat(received.Select(s => s.PartyName)).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return PartyLedger.Build(q.PartyId, name, "customer", q.From, q.To, openingDocs - openingCash, lines, debitRaises: true);
    }

    public async Task<PartyLedgerDto> Handle(GetVendorLedger q, CancellationToken ct)
    {
        var docs = bills.Query(new Specification<Bill>(b => b.PartyId == q.PartyId && b.BillDate <= q.To
            && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid)
            && (q.LegalEntityId == null || b.LegalEntityId == q.LegalEntityId)));
        var cash = Settled(InstrumentDirection.Issued, q.PartyId, q.To, q.LegalEntityId);

        var openingDocs = await docs.Where(b => b.BillDate < q.From).SumAsync(b => (decimal?)b.Total, ct) ?? 0m;
        var openingCash = await cash.Where(s => s.Date < q.From).SumAsync(s => (decimal?)s.Amount, ct) ?? 0m;

        var inRange = await docs.Where(b => b.BillDate >= q.From)
            .Select(b => new { b.Id, b.Number, b.BillDate, b.PartyName, b.CurrencyCode, b.Total, b.VendorReference }).ToListAsync(ct);
        var paid = await cash.Where(s => s.Date >= q.From)
            .Select(s => new { s.Id, s.Number, s.Date, s.PartyName, s.CurrencyCode, s.Amount, s.Reference }).ToListAsync(ct);

        var lines = inRange.Select(b => new PartyLedgerLine(b.BillDate, "bill", b.Id, b.Number, b.VendorReference, b.CurrencyCode, 0, b.Total, 0))
            .Concat(paid.Select(s => new PartyLedgerLine(s.Date, "payment", s.Id, s.Number, s.Reference, s.CurrencyCode, s.Amount, 0, 0)));
        var name = inRange.Select(b => b.PartyName).Concat(paid.Select(s => s.PartyName)).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return PartyLedger.Build(q.PartyId, name, "vendor", q.From, q.To, openingDocs - openingCash, lines, debitRaises: false);
    }

    /// <summary>Receipts (received) or payments (issued) of the party up to a date; voided ones are left out.</summary>
    private IQueryable<Settlement> Settled(InstrumentDirection direction, Guid partyId, DateOnly to, Guid? entityId)
        => settlements.Query(new Specification<Settlement>(s => s.Direction == direction && s.PartyId == partyId && s.Date <= to
            && s.Status == DocumentStatus.Issued && (entityId == null || s.LegalEntityId == entityId)));
}
