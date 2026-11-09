using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Dashboard;

internal sealed class GetOwnerDashboardHandler(
    IJournalEntryRepository journals, IAccountRepository accounts, IInvoiceRepository invoices, IBillRepository bills,
    IBusinessUnitRepository units, IBudgetRepository budgets, ILegalEntityRepository legalEntities, IEnumerable<IOwnerDashboardSection> moduleSections, IEnumerable<IDashboardContributor> contributors,
    IModuleAccess modules, ISettingsService settings, ICurrentUser user, IClock clock, ISender sender) : IQueryHandler<GetOwnerDashboardQuery, OwnerDashboardDto>
{
    public async Task<OwnerDashboardDto> Handle(GetOwnerDashboardQuery q, CancellationToken ct)
    {
        // 1. The period, how every account is classified and the posted lines up to the end of the period.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var (from, to) = DashboardRange.Resolve(q.From, q.To, today);
        var entity = q.LegalEntityId;
        var scope = new OwnerScope(entity, from, to, today);

        var info = await accounts.ListClassificationsAsync(ct);
        var byId = info.ToDictionary(a => a.Id);
        var lines = await journals.ListPostedLinesUpToAsync(to, entity, ct);
        var inPeriod = lines.Where(l => l.Date >= from).ToList();

        bool IsCostOfSales(Guid id) => byId[id].ReportingGroup == "Cost of sales" || byId[id].SystemRole == "cogs";
        decimal Income(IEnumerable<PostedJournalLine> ls) => ls.Where(l => byId.TryGetValue(l.AccountId, out var a) && a.Type == AccountType.Income).Sum(l => l.Credit - l.Debit);
        decimal CostOfSales(IEnumerable<PostedJournalLine> ls) => ls.Where(l => byId.TryGetValue(l.AccountId, out var a) && a.Type == AccountType.Expense && IsCostOfSales(l.AccountId)).Sum(l => l.Debit - l.Credit);
        decimal Overheads(IEnumerable<PostedJournalLine> ls) => ls.Where(l => byId.TryGetValue(l.AccountId, out var a) && a.Type == AccountType.Expense && !IsCostOfSales(l.AccountId)).Sum(l => l.Debit - l.Credit);
        bool IsCash(Guid id) => byId.TryGetValue(id, out var a) && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole is "cash" or "bank");

        var periods = DashboardRange.Buckets(from, to, DashboardRange.Granularity(from, to));
        var labels = periods.Select(p => DashboardRange.Label(p, DashboardRange.Granularity(from, to))).ToList();
        List<PostedJournalLine> In(DashboardBucket p) => inPeriod.Where(l => l.Date >= p.From && l.Date <= p.To).ToList();

        // 2. Revenue, expenses and profit.
        var revenue = Income(inPeriod);
        var cost = CostOfSales(inPeriod);
        var overheads = Overheads(inPeriod);
        var gross = revenue - cost;
        var net = gross - overheads;
        var sections = new List<OwnerSection>
        {
            new("revenue", "Revenue and expenses",
                [new("revenue", "Revenue", revenue, Link: "/finance/reports"), new("expenses", "Expenses", cost + overheads, Link: "/finance/reports")],
                Chart: new(labels, [new("Revenue", periods.Select(p => Income(In(p))).ToList()), new("Expenses", periods.Select(p => CostOfSales(In(p)) + Overheads(In(p))).ToList())])),
            new("gross-profit", "Profit",
                [new("cost-of-sales", "Cost of sales", cost), new("gross-profit", "Gross profit", gross), new("gross-margin", "Gross margin", revenue == 0 ? 0 : Math.Round(gross / revenue * 100, 1), "percent"), new("net-profit", "Net profit", net, Link: "/finance/reports")]),
        };

        // 3. Cash flow.
        var opening = lines.Where(l => l.Date < from && IsCash(l.AccountId)).Sum(l => l.Debit - l.Credit);
        var cashMoves = inPeriod.Where(l => IsCash(l.AccountId)).ToList();
        var cashIn = cashMoves.Sum(l => l.Debit);
        var cashOut = cashMoves.Sum(l => l.Credit);
        sections.Add(new("cash", "Cash flow",
            [new("cash-opening", "Opening cash", opening), new("cash-in", "Cash in", cashIn), new("cash-out", "Cash out", cashOut), new("cash-closing", "Closing cash", opening + cashIn - cashOut, Link: "/finance/bank")],
            Chart: new(labels, [new("Cash in", periods.Select(p => In(p).Where(l => IsCash(l.AccountId)).Sum(l => l.Debit)).ToList()), new("Cash out", periods.Select(p => In(p).Where(l => IsCash(l.AccountId)).Sum(l => l.Credit)).ToList())])));

        // 4. Receivables and payables, for callers who may read them.
        if (user.HasPermission(CorePermissions.InvoicesRead))
        {
            var open = await invoices.ListOpenBalancesAsync(entity, ct);
            decimal Balance(bool credit, decimal total, decimal paid) => (credit ? -1 : 1) * (total - paid);
            var top = open.GroupBy(i => i.PartyName ?? "").Select(g => new { Name = g.Key, Value = g.Sum(i => Balance(i.IsCreditNote, i.Total, i.Paid)) }).OrderByDescending(g => g.Value).Take(5).ToList();
            sections.Add(new("receivables", "Receivables",
                [new("receivables", "Owed by customers", open.Sum(i => Balance(i.IsCreditNote, i.Total, i.Paid)), Link: "/finance/invoices"), new("receivables-overdue", "Overdue", open.Where(i => i.DueDate < today).Sum(i => Balance(i.IsCreditNote, i.Total, i.Paid)))],
                new(["Customer", "Balance"], top.Select(t => (IReadOnlyList<string>)[t.Name, t.Value.ToString("0.00")]).ToList(), ["text", "money"])));
        }

        if (user.HasPermission(CorePermissions.BillsRead))
        {
            var open = await bills.ListOpenBalancesAsync(entity, ct);
            var top = open.GroupBy(b => b.PartyName ?? "").Select(g => new { Name = g.Key, Value = g.Sum(b => b.Total - b.Paid) }).OrderByDescending(g => g.Value).Take(5).ToList();
            sections.Add(new("payables", "Payables",
                [new("payables", "Owed to vendors", open.Sum(b => b.Total - b.Paid), Link: "/finance/bills"), new("payables-overdue", "Overdue", open.Where(b => b.DueDate < today).Sum(b => b.Total - b.Paid))],
                new(["Vendor", "Balance"], top.Select(t => (IReadOnlyList<string>)[t.Name, t.Value.ToString("0.00")]).ToList(), ["text", "money"])));
        }

        // 5. Branch comparison, when lines carry a branch.
        var branchRows = inPeriod.Where(l => l.BusinessUnitId is not null).GroupBy(l => l.BusinessUnitId!.Value).Select(g => new { Unit = g.Key, Revenue = Income(g), Cost = CostOfSales(g) + Overheads(g) }).ToList();
        if (branchRows.Count > 0)
        {
            var ids = branchRows.Select(r => r.Unit).ToList();
            var names = await units.GetNamesAsync(ids, ct);
            var ordered = branchRows.OrderByDescending(r => r.Revenue).ToList();
            var untagged = inPeriod.Where(l => l.BusinessUnitId is null).ToList();
            sections.Add(new("branch", "Branch comparison", [],
                new(["Branch", "Revenue", "Costs", "Result"], ordered.Select(r => (IReadOnlyList<string>)[names.GetValueOrDefault(r.Unit) ?? "?", r.Revenue.ToString("0.00"), r.Cost.ToString("0.00"), (r.Revenue - r.Cost).ToString("0.00")]).ToList(), ["text", "money", "money", "money"]),
                new(ordered.Select(r => names.GetValueOrDefault(r.Unit) ?? "?").ToList(), [new("Revenue", ordered.Select(r => r.Revenue).ToList()), new("Costs", ordered.Select(r => r.Cost).ToList())]),
                untagged.Count == 0 ? null : $"Transactions without a branch are left out: revenue {Income(untagged):N0}, costs {CostOfSales(untagged) + Overheads(untagged):N0}."));
        }

        // 6. Budget against actual, for the first active budget of the year.
        if (user.HasPermission(CorePermissions.BudgetsRead))
        {
            var budget = await budgets.FindFirstActiveIdAsync(to.Year, entity, ct);
            if (budget is not null)
            {
                var report = await sender.Send(new ERP.Application.Finance.GetBudgetVsActual(budget.Value, from.Year == to.Year ? from.Month : 1, to.Month), ct);
                var worst = report.Rows.Where(r => r.Budget != 0).OrderBy(r => r.Variance).Take(6).ToList();
                sections.Add(new("budget", "Budget against actual",
                    [new("budget-income", "Income budget", report.Rows.Where(r => r.Type == AccountType.Income).Sum(r => r.Budget)), new("actual-income", "Income actual", report.Rows.Where(r => r.Type == AccountType.Income).Sum(r => r.Actual)),
                     new("budget-expense", "Expense budget", report.Rows.Where(r => r.Type == AccountType.Expense).Sum(r => r.Budget)), new("actual-expense", "Expense actual", report.Rows.Where(r => r.Type == AccountType.Expense).Sum(r => r.Actual))],
                    new(["Account", "Budget", "Actual", "Variance"], worst.Select(r => (IReadOnlyList<string>)[$"{r.Code} {r.Name}", r.Budget.ToString("0.00"), r.Actual.ToString("0.00"), r.Variance.ToString("0.00")]).ToList(), ["text", "money", "money", "money"])));
            }
        }

        // 7. The enabled modules' own sections.
        foreach (var section in moduleSections)
            if (await modules.IsEnabledAsync(section.ModuleKey, ct))
                sections.AddRange(await section.BuildAsync(scope, ct));

        // The business's own cards, from the same contributors as the overview, so an industry's measures appear here without extra code.
        var kpis = new List<OwnerCard>();
        var dashboardScope = new DashboardScope(entity, today) { From = from, To = to };
        foreach (var contributor in contributors.Where(c => c.ModuleKey != "core"))
        {
            if (!await modules.IsEnabledAsync(contributor.ModuleKey, ct)) continue;
            var part = await contributor.BuildAsync(dashboardScope, ct);
            kpis.AddRange(part.Kpis.Select(k => new OwnerCard(k.Key, k.Label, k.Value, k.Format == "currency" ? "money" : k.Format, k.Caption, k.Link)));
        }
        if (kpis.Count > 0) sections.Add(new("business", "Business measures", kpis));

        // 8. The owner's chosen focus first; one currency is shown only when every entity in scope shares it.
        var focus = await FocusAsync(ct);
        var ordered2 = sections.Select((s, n) => (Section: s, Rank: focus.IndexOf(s.Key) is var r && r >= 0 ? r : focus.Count + n)).OrderBy(x => x.Rank).Select(x => x.Section).ToList();
        var currencies = await legalEntities.ListBaseCurrenciesAsync(entity, ct);
        return new OwnerDashboardDto(from, to, currencies.Count == 1 ? currencies[0] : null, ordered2);
    }

    private async Task<List<string>> FocusAsync(CancellationToken ct)
    {
        var raw = await settings.GetRawAsync("core.dashboardFocus", ct: ct);
        return string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<List<string>>(raw) ?? [];
    }
}
