using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

// ---- Tax summary -------------------------------------------------------------------------------------------------------------

public sealed record TaxSummaryRow(string Section, Guid? TaxCodeId, string Code, string Name, decimal Rate, decimal Net, decimal Tax);
public sealed record TaxSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<TaxSummaryRow> Rows, decimal OutputTax, decimal InputTax, decimal NetPayable, decimal NonRecoverableTax);

/// <summary>Tax on issued invoices and credit notes (output) against recoverable tax on posted bills (input) for a period. The per-country return layout is a country pack concern; this is the neutral base.</summary>
public sealed record GetTaxSummary(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<TaxSummaryDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

// ---- Cash movements ----------------------------------------------------------------------------------------------------------

public sealed record CashMovementRow(Guid AccountId, string Code, string Name, decimal Opening, decimal Inflows, decimal Outflows, decimal Closing);
public sealed record CashSourceRow(string Source, decimal Inflows, decimal Outflows);
public sealed record CashMovementsDto(DateOnly From, DateOnly To, IReadOnlyList<CashMovementRow> Accounts, IReadOnlyList<CashSourceRow> BySource, decimal Opening, decimal Inflows, decimal Outflows, decimal Closing);
public sealed record GetCashMovements(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CashMovementsDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

// ---- Cash forecast -----------------------------------------------------------------------------------------------------------

/// <summary>An expected cash movement on a date: positive = money in, negative = money out.</summary>
public sealed record ForecastItem(DateOnly Date, decimal Amount);

/// <summary>Where expected cash comes from. Core supplies cheques, receivables and bills; modules add their own (payroll, loan instalments).</summary>
public interface ICashForecastSource
{
    string Key { get; }
    string Label { get; }
    /// <summary>Null for core sources; a module key for sources that only apply while that module is enabled.</summary>
    string? ModuleKey { get; }
    Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? legalEntityId, DateOnly asOf, DateOnly horizon, CancellationToken ct);
}

public sealed record ForecastWeek(DateOnly WeekOf, IReadOnlyDictionary<string, decimal> Amounts, decimal Net, decimal Projected);
public sealed record CashForecastDto(DateOnly AsOf, decimal CashToday, IReadOnlyList<ForecastSourceInfo> Sources, IReadOnlyList<ForecastWeek> Weeks, decimal EndBalance, decimal LowestBalance);
public sealed record ForecastSourceInfo(string Key, string Label);
/// <summary>Cash today plus expected receipts and payments by week. Overdue items fall into the first week.</summary>
public sealed record GetCashForecast(int Weeks, Guid? LegalEntityId) : IQuery<CashForecastDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

internal sealed class CashReportHandlers(
    IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<TaxCode> taxCodes, IReadRepository<JournalEntry> journals, IReadRepository<Account> accounts,
    IEnumerable<ICashForecastSource> sources, IModuleAccess modules, IClock clock) :
    IRequestHandler<GetTaxSummary, TaxSummaryDto>,
    IRequestHandler<GetCashMovements, CashMovementsDto>,
    IRequestHandler<GetCashForecast, CashForecastDto>
{
    public async Task<TaxSummaryDto> Handle(GetTaxSummary q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var sales = await invoices.Query(new Specification<Invoice>(i => i.InvoiceDate >= q.From && i.InvoiceDate <= q.To && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Voided && (entity == null || i.LegalEntityId == entity)))
            .SelectMany(i => i.Lines, (i, l) => new { i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount }).ToListAsync(ct);
        var purchases = await bills.Query(new Specification<Bill>(b => b.BillDate >= q.From && b.BillDate <= q.To && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid) && (entity == null || b.LegalEntityId == entity)))
            .SelectMany(b => b.Lines, (b, l) => new { l.TaxCodeId, l.Net, l.TaxAmount, l.TaxRecoverable }).ToListAsync(ct);
        var codes = await taxCodes.Query().Select(t => new { t.Id, t.Code, t.Name, t.Rate }).ToDictionaryAsync(t => t.Id, ct);

        TaxSummaryRow Row(string section, Guid? id, decimal net, decimal tax)
            => id is { } k && codes.TryGetValue(k, out var c) ? new(section, k, c.Code, c.Name, c.Rate, net, tax) : new(section, null, "-", "No tax", 0, net, tax);

        var rows = new List<TaxSummaryRow>();
        rows.AddRange(sales.GroupBy(s => s.TaxCodeId).Select(g => Row("Sales", g.Key, g.Sum(x => x.IsCreditNote ? -x.Net : x.Net), g.Sum(x => x.IsCreditNote ? -x.TaxAmount : x.TaxAmount))));
        rows.AddRange(purchases.Where(p => p.TaxRecoverable).GroupBy(p => p.TaxCodeId).Select(g => Row("Purchases", g.Key, g.Sum(x => x.Net), g.Sum(x => x.TaxAmount))));
        rows = rows.OrderBy(r => r.Section).ThenBy(r => r.Code).ToList();
        var output = rows.Where(r => r.Section == "Sales").Sum(r => r.Tax); var input = rows.Where(r => r.Section == "Purchases").Sum(r => r.Tax);
        return new TaxSummaryDto(q.From, q.To, rows, output, input, output - input, purchases.Where(p => !p.TaxRecoverable).Sum(p => p.TaxAmount));
    }

    public async Task<CashMovementsDto> Handle(GetCashMovements q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var cash = await accounts.Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).OrderBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.Name }).ToListAsync(ct);
        var ids = cash.Select(c => c.Id).ToList();
        var moves = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= q.To && (entity == null || j.LegalEntityId == entity)))
            .SelectMany(j => j.Lines.Where(l => ids.Contains(l.AccountId)), (j, l) => new { l.AccountId, j.Date, j.SourceType, l.Debit, l.Credit }).ToListAsync(ct);
        var rows = cash.Select(c =>
        {
            var mine = moves.Where(m => m.AccountId == c.Id).ToList();
            var opening = mine.Where(m => m.Date < q.From).Sum(m => m.Debit - m.Credit);
            var within = mine.Where(m => m.Date >= q.From).ToList();
            var inflow = within.Sum(m => m.Debit); var outflow = within.Sum(m => m.Credit);
            return new CashMovementRow(c.Id, c.Code, c.Name, opening, inflow, outflow, opening + inflow - outflow);
        }).Where(r => r.Opening != 0 || r.Inflows != 0 || r.Outflows != 0).ToList();
        var bySource = moves.Where(m => m.Date >= q.From).GroupBy(m => m.SourceType).Select(g => new CashSourceRow(g.Key, g.Sum(m => m.Debit), g.Sum(m => m.Credit))).OrderBy(s => s.Source).ToList();
        return new CashMovementsDto(q.From, q.To, rows, bySource, rows.Sum(r => r.Opening), rows.Sum(r => r.Inflows), rows.Sum(r => r.Outflows), rows.Sum(r => r.Closing));
    }

    public async Task<CashForecastDto> Handle(GetCashForecast q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday of this week
        var weeks = Math.Clamp(q.Weeks, 1, 52);
        var horizon = start.AddDays(weeks * 7 - 1);
        var cash = (await Handle(new GetCashMovements(DateOnly.FromDateTime(clock.UtcNow), today, q.LegalEntityId), ct)).Closing;

        var active = new List<(ICashForecastSource S, IReadOnlyList<ForecastItem> Items)>();
        foreach (var s in sources)
        {
            if (s.ModuleKey is { } key && !await modules.IsEnabledAsync(key, ct)) continue;
            active.Add((s, await s.ItemsAsync(q.LegalEntityId, today, horizon, ct)));
        }
        var result = new List<ForecastWeek>(); var balance = cash;
        for (var w = 0; w < weeks; w++)
        {
            var from = start.AddDays(w * 7); var to = from.AddDays(6);
            var amounts = active.ToDictionary(a => a.S.Key, a => a.Items.Where(i => (w == 0 ? i.Date <= to : i.Date >= from && i.Date <= to)).Sum(i => i.Amount));
            var net = amounts.Values.Sum(); balance += net;
            result.Add(new ForecastWeek(from, amounts, net, balance));
        }
        return new CashForecastDto(today, cash, active.Select(a => new ForecastSourceInfo(a.S.Key, a.S.Label)).ToList(), result, balance, Math.Min(cash, result.Min(w => w.Projected)));
    }
}

/// <summary>Cheques in custody (by the date on the cheque) and cheques we issued (by their date) are expected to move cash on those dates.</summary>
public sealed class ChequeForecastSource(IReadRepository<PaymentInstrument> instruments) : ICashForecastSource
{
    public string Key => "cheques";
    public string Label => "Cheques";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await instruments.Query(new Specification<PaymentInstrument>(i => (i.Status == InstrumentStatus.InCustody || i.Status == InstrumentStatus.Deposited || i.Status == InstrumentStatus.Issued) && i.InstrumentDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.Direction, i.Amount, i.InstrumentDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.InstrumentDate, r.Direction == InstrumentDirection.Received ? r.Amount : -r.Amount)).ToList();
    }
}

/// <summary>Open invoices by due date (credit notes reduce it). Cheques already received are not counted again because they settled their invoices.</summary>
public sealed class ReceivablesForecastSource(IReadRepository<Invoice> invoices) : ICashForecastSource
{
    public string Key => "receivables";
    public string Label => "Receivables";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.DueDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.IsCreditNote, i.Total, i.Paid, i.DueDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, (r.IsCreditNote ? -1 : 1) * (r.Total - r.Paid))).ToList();
    }
}

public sealed class PayablesForecastSource(IReadRepository<Bill> bills) : ICashForecastSource
{
    public string Key => "bills";
    public string Label => "Bills";
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.DueDate <= horizon && (entityId == null || b.LegalEntityId == entityId)))
            .Select(b => new { b.Total, b.Paid, b.DueDate }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, -(r.Total - r.Paid))).ToList();
    }
}
