using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Dashboard;

/// <summary>
/// The industry-neutral cards: receivables, collections, cash, cheques, payables, approvals and the result for the period.
/// Billed, collected, net result and expenses cover the scope's period; cash is the balance at the end of the period;
/// receivables, ageing, payables, cheques and approvals are open items today.
/// </summary>
public sealed class FinanceDashboard(
    IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<Settlement> settlements, IReadRepository<PaymentInstrument> instruments,
    IReadRepository<JournalEntry> journals, IReadRepository<Account> accounts, IReadRepository<ApprovalRequest> approvals, ISettingsService settings, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => "core";

    private sealed record Line(Guid AccountId, DateOnly Date, decimal Debit, decimal Credit);

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>(); var charts = new List<DashboardChart>(); var alerts = new List<DashboardAlert>();
        var canSee = (string p) => user.HasPermission(p);
        var (from, to) = (scope.From, scope.To);

        if (canSee(CorePermissions.InvoicesRead))
        {
            var open = await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && (entity == null || i.LegalEntityId == entity)))
                .Select(i => new { i.IsCreditNote, i.Total, i.Paid, i.DueDate }).ToListAsync(ct);
            var sign = (bool credit) => credit ? -1m : 1m;
            var receivable = open.Sum(i => sign(i.IsCreditNote) * (i.Total - i.Paid));
            var overdue = open.Where(i => i.DueDate < scope.Today).Sum(i => sign(i.IsCreditNote) * (i.Total - i.Paid));
            kpis.Add(new("receivables", "Receivables", receivable, "money", $"overdue {overdue:N0}", 30, "/finance/invoices"));

            var billed = await invoices.Query(new Specification<Invoice>(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Voided && (entity == null || i.LegalEntityId == entity)))
                .Select(i => new { i.InvoiceDate, i.IsCreditNote, i.Net }).ToListAsync(ct);
            var collected = canSee(CorePermissions.ReceiptsRead)
                ? await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Date >= from && s.Date <= to && (entity == null || s.LegalEntityId == entity)))
                    .Select(s => new { s.Date, s.Amount }).ToListAsync(ct)
                : [];
            // whole weeks / months / quarters counted back from the end of the period, so no stub bar at either end
            var granularity = DashboardRange.Granularity(from, to);
            var periods = DashboardRange.Buckets(from, to, granularity);
            var billedByBucket = periods.Select(p => billed.Where(b => b.InvoiceDate >= p.From && b.InvoiceDate <= p.To).Sum(b => b.IsCreditNote ? -b.Net : b.Net)).ToList();
            var collectedByBucket = periods.Select(p => collected.Where(c => c.Date >= p.From && c.Date <= p.To).Sum(c => c.Amount)).ToList();
            kpis.Add(new("collected", "Collected in period", collectedByBucket.Sum(), "money", $"billed {billedByBucket.Sum():N0}", 40, "/finance/receipts"));
            charts.Add(new("billed-collected", "Billed vs collected", "bar", periods.Select(p => DashboardRange.Label(p, granularity)).ToList(),
                [new("Billed", billedByBucket), new("Collected", collectedByBucket)], "Invoices net of credit notes, and receipts, for the selected period", 10, periods, granularity));

            var buckets = new decimal[5];
            foreach (var i in open)
            {
                var late = scope.Today.DayNumber - i.DueDate.DayNumber;
                buckets[late <= 0 ? 0 : late <= 30 ? 1 : late <= 60 ? 2 : late <= 90 ? 3 : 4] += sign(i.IsCreditNote) * (i.Total - i.Paid);
            }
            charts.Add(new("ar-ageing", "Receivables ageing", "hbar", ["Current", "1-30", "31-60", "61-90", "90+"], [new("Open balance", buckets)], $"Open invoice balances by days past due, total {receivable:N2}", 20));
        }

        if (canSee(CorePermissions.BillsRead))
        {
            var payable = (await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && (entity == null || b.LegalEntityId == entity)))
                .Select(b => new { b.Total, b.Paid }).ToListAsync(ct)).Sum(b => b.Total - b.Paid);
            kpis.Add(new("payables", "Payables", payable, "money", "approved, unpaid bills", 70, "/finance/bills"));
        }

        if (canSee(CorePermissions.BankRead))
        {
            var cashIds = await accounts.Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).Select(a => a.Id).ToListAsync(ct);
            var moves = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entity == null || j.LegalEntityId == entity)))
                .SelectMany(j => j.Lines.Where(l => cashIds.Contains(l.AccountId)), (j, l) => new { l.Debit, l.Credit }).ToListAsync(ct);
            kpis.Add(new("cash", "Cash and bank", moves.Sum(m => m.Debit - m.Credit), "money", $"all bank and cash accounts, at {to.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}", 60, "/finance/bank",
                "kpi.cash.caption", new Dictionary<string, string> { ["date"] = to.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) }));
        }

        if (canSee(CorePermissions.ChequesRead))
        {
            var held = await instruments.Query(new Specification<PaymentInstrument>(i => i.Direction == InstrumentDirection.Received && (i.Status == InstrumentStatus.InCustody || i.Status == InstrumentStatus.Deposited || i.Status == InstrumentStatus.Bounced) && (entity == null || i.LegalEntityId == entity)))
                .Select(i => new { i.Status, i.Amount, i.InstrumentDate, i.InstrumentNo, i.PartyName }).ToListAsync(ct);
            var custody = held.Where(h => h.Status == InstrumentStatus.InCustody).ToList();
            kpis.Add(new("pdcs", "Cheques in custody", custody.Sum(h => h.Amount), "money", $"{custody.Count} cheques, not yet cash", 65, "/finance/cheques"));
            var dueDays = await settings.GetAsync<int?>("finance.chequeDueAlertDays", null, ct) ?? 7;
            var due = custody.Where(h => h.InstrumentDate <= scope.Today.AddDays(dueDays)).ToList();
            if (due.Count > 0) alerts.Add(new("warning", "Cheques", "action", $"{due.Count} cheque(s) worth {due.Sum(h => h.Amount):N2} are due for deposit by {scope.Today.AddDays(dueDays):dd MMM}.", "/finance/cheques", 20));
            foreach (var b in held.Where(h => h.Status == InstrumentStatus.Bounced).Take(5))
                alerts.Add(new("critical", "Cheques", "action", $"Cheque {b.InstrumentNo} from {b.PartyName} ({b.Amount:N2}) bounced. Arrange a replacement.", "/finance/cheques", 10));
        }

        if (canSee(CorePermissions.ReportsRead))
        {
            var lines = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date >= from && j.Date <= to && (entity == null || j.LegalEntityId == entity)))
                .SelectMany(j => j.Lines, (j, l) => new Line(l.AccountId, j.Date, l.Debit, l.Credit)).ToListAsync(ct);
            var info = await accounts.Query(new Specification<Account>(a => a.Type == AccountType.Income || a.Type == AccountType.Expense)).Select(a => new { a.Id, a.Type, a.ReportingGroup, a.Name }).ToListAsync(ct);
            var income = 0m; var expense = 0m; var byGroup = new Dictionary<string, decimal>();
            foreach (var l in lines)
            {
                var a = info.FirstOrDefault(x => x.Id == l.AccountId);
                if (a is null) continue;
                if (a.Type == AccountType.Income) income += l.Credit - l.Debit;
                else { var v = l.Debit - l.Credit; expense += v; var g = a.ReportingGroup ?? a.Name; byGroup[g] = byGroup.GetValueOrDefault(g) + v; }
            }
            kpis.Add(new("net-result", "Net result for period", income - expense, "money", "income less expenses", 50, "/finance/reports"));
            var top = byGroup.Where(g => g.Value != 0).OrderByDescending(g => g.Value).Take(8).ToList();
            charts.Add(new("expenses", "Expenses for period", "hbar", top.Select(g => g.Key).ToList(), [new("Expenses", top.Select(g => g.Value).ToList())], $"total {expense:N2}", 40));
        }

        var pending = await approvals.CountAsync(a => a.Status == ApprovalStatus.Pending && (user.HasPermission(CorePermissions.ApprovalsDecide) ? a.RequestedBy != user.UserId : a.RequestedBy == user.UserId), ct);
        kpis.Add(new("approvals", "Pending approvals", pending, "number", user.HasPermission(CorePermissions.ApprovalsDecide) ? "waiting for your decision" : "your requests waiting", 90, "/approvals"));
        if (pending > 0 && user.HasPermission(CorePermissions.ApprovalsDecide)) alerts.Add(new("info", "Approvals", "info", $"{pending} request(s) are waiting for your approval.", "/approvals", 90));

        return new DashboardContribution(kpis, charts, alerts);
    }
}
