using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record RentRollRow(
    Guid PropertyId, string Property, Guid UnitId, string Unit, string? UnitType, decimal AreaSqft, Guid LeaseId, string? LeaseNumber, string Tenant, DateOnly Start, DateOnly Expiry, int DaysLeft,
    decimal AnnualRent, decimal RentPerSqft, string VatStatus, decimal PdcsHeld, decimal Outstanding);
public sealed record RentRollDto(DateOnly AsOf, IReadOnlyList<RentRollRow> Rows, decimal TotalAnnualRent, decimal TotalOutstanding);

public sealed record OccupancyRow(Guid PropertyId, string Property, Guid LegalEntityId, int TotalUnits, int Registered, int Occupied, int Vacant, int Other, decimal OccupancyPercent, decimal ContractedRent, decimal VacantAskingRent);
public sealed record OccupancyDto(IReadOnlyList<OccupancyRow> Rows, decimal OccupancyPercent, decimal ContractedRent, decimal VacancyAskingRent, int VacantUnits);

public sealed record PerformanceRow(Guid PropertyId, string Property, Guid LegalEntityId, decimal Value, decimal OccupancyPercent, decimal Income, decimal OperatingExpenses, decimal Noi, decimal AnnualisedYieldPercent, decimal Billed, decimal Collected, decimal CollectionRatePercent);
public sealed record PerformanceDto(DateOnly From, DateOnly To, IReadOnlyList<PerformanceRow> Rows, PerformanceRow Total);

public sealed record ExpiryRow(string Label, DateOnly? Month, int Leases, decimal AnnualRentAtRisk, bool PastEnd);
public sealed record LeaseExpiriesDto(IReadOnlyList<ExpiryRow> Rows, int TotalLeases, decimal TotalRentAtRisk);

public sealed record MaintenanceRow(Guid PropertyId, string Property, string Category, int WorkOrders, int Open, decimal Estimated, decimal Actual);
public sealed record MaintenanceCostsDto(DateOnly From, DateOnly To, IReadOnlyList<MaintenanceRow> Rows, int WorkOrders, int Open, decimal Estimated, decimal Actual);

public sealed record CommissionRow(string Agent, int Deals, decimal DealValue, decimal GrossCommission, decimal AgentShare, decimal TeamLeadShare, decimal CompanyShare, decimal PaidOut);
public sealed record CommissionsDto(DateOnly From, DateOnly To, IReadOnlyList<CommissionRow> Rows, CommissionRow Total);

public sealed record GetRentRoll(DateOnly AsOf, Guid[]? PropertyIds, Guid? LegalEntityId) : IQuery<RentRollDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetOccupancyReport(Guid? LegalEntityId) : IQuery<OccupancyDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
/// <summary>Income (rent invoiced, net of credit notes) less property operating expenses (work order actual costs), with yield annualised against the indicative value.</summary>
public sealed record GetPortfolioPerformance(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<PerformanceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetLeaseExpiries(int Months, Guid? LegalEntityId) : IQuery<LeaseExpiriesDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetMaintenanceCosts(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<MaintenanceCostsDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetCommissionsReport(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CommissionsDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

internal sealed class ReportHandlers(
    IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<UnitType> unitTypes, IReadRepository<Lease> leases, IReadRepository<RentInstalment> instalments,
    IReadRepository<WorkOrder> workOrders, IReadRepository<BrokerageDeal> deals, IReadRepository<Party> parties, IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements,
    IReadRepository<PaymentInstrument> instruments, IClock clock) :
    IRequestHandler<GetRentRoll, RentRollDto>,
    IRequestHandler<GetOccupancyReport, OccupancyDto>,
    IRequestHandler<GetPortfolioPerformance, PerformanceDto>,
    IRequestHandler<GetLeaseExpiries, LeaseExpiriesDto>,
    IRequestHandler<GetMaintenanceCosts, MaintenanceCostsDto>,
    IRequestHandler<GetCommissionsReport, CommissionsDto>
{
    public async Task<RentRollDto> Handle(GetRentRoll q, CancellationToken ct)
    {
        var entity = q.LegalEntityId; var propertyIds = q.PropertyIds ?? [];
        var active = await leases.Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && (entity == null || l.LegalEntityId == entity) && (propertyIds.Length == 0 || propertyIds.Contains(l.PropertyId))))
            .Select(l => new { l.Id, l.Number, l.UnitId, l.PropertyId, l.LesseePartyId, l.StartDate, l.EndDate, l.AnnualRent, l.TaxCodeId }).ToListAsync(ct);
        var unitIds = active.Select(l => l.UnitId).ToList(); var propIds = active.Select(l => l.PropertyId).Distinct().ToList(); var partyIds = active.Select(l => l.LesseePartyId).Distinct().ToList();
        var unitMap = await units.Query(new Specification<Unit>(u => unitIds.Contains(u.Id))).Select(u => new { u.Id, u.UnitNumber, u.AreaSqft, u.UnitTypeId, u.Usage }).ToDictionaryAsync(u => u.Id, ct);
        var typeIds = unitMap.Values.Select(u => u.UnitTypeId).Distinct().ToList();
        var typeNames = await unitTypes.Query(new Specification<UnitType>(t => typeIds.Contains(t.Id))).Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var propNames = await properties.Query(new Specification<Property>(p => propIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var tenants = await parties.Query(new Specification<Party>(p => partyIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var pdcs = (await instruments.Query(new Specification<PaymentInstrument>(i => i.Direction == InstrumentDirection.Received && i.Status == InstrumentStatus.InCustody && i.PartyId != null && partyIds.Contains(i.PartyId.Value)))
            .Select(i => new { i.PartyId, i.Amount }).ToListAsync(ct)).GroupBy(i => i.PartyId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
        var owed = (await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && partyIds.Contains(i.PartyId)))
            .Select(i => new { i.PartyId, i.IsCreditNote, i.Total, i.Paid }).ToListAsync(ct)).GroupBy(i => i.PartyId).ToDictionary(g => g.Key, g => g.Sum(i => (i.IsCreditNote ? -1 : 1) * (i.Total - i.Paid)));

        var rows = active.Select(l =>
        {
            var u = unitMap[l.UnitId];
            var vat = l.TaxCodeId is not null ? "override" : u.Usage == UnitUsage.Commercial ? "standard" : "exempt";
            return new RentRollRow(l.PropertyId, propNames.GetValueOrDefault(l.PropertyId, "?"), l.UnitId, u.UnitNumber, typeNames.GetValueOrDefault(u.UnitTypeId), u.AreaSqft, l.Id, l.Number, tenants.GetValueOrDefault(l.LesseePartyId, "?"),
                l.StartDate, l.EndDate, l.EndDate.DayNumber - q.AsOf.DayNumber, l.AnnualRent, u.AreaSqft > 0 ? Math.Round(l.AnnualRent / u.AreaSqft, 2) : 0, vat, pdcs.GetValueOrDefault(l.LesseePartyId), owed.GetValueOrDefault(l.LesseePartyId));
        }).OrderBy(r => r.Property).ThenBy(r => r.Unit).ToList();
        return new RentRollDto(q.AsOf, rows, rows.Sum(r => r.AnnualRent), rows.Sum(r => r.Outstanding));
    }

    public async Task<OccupancyDto> Handle(GetOccupancyReport q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var props = await properties.Query(new Specification<Property>(p => p.IsActive && (entity == null || p.LegalEntityId == entity))).OrderBy(p => p.Name).Select(p => new { p.Id, p.Name, p.LegalEntityId, p.TotalUnits }).ToListAsync(ct);
        var unitRows = await units.Query(new Specification<Unit>(u => entity == null || u.LegalEntityId == entity)).Select(u => new { u.Id, u.PropertyId, u.Status, u.AskingRent }).ToListAsync(ct);
        var rent = (await leases.Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && (entity == null || l.LegalEntityId == entity))).Select(l => new { l.PropertyId, l.AnnualRent }).ToListAsync(ct))
            .GroupBy(l => l.PropertyId).ToDictionary(g => g.Key, g => g.Sum(l => l.AnnualRent));
        var rows = props.Select(p =>
        {
            var mine = unitRows.Where(u => u.PropertyId == p.Id).ToList();
            var occupied = mine.Count(u => u.Status == UnitStatus.Occupied); var vacant = mine.Count(u => u.Status == UnitStatus.Vacant);
            return new OccupancyRow(p.Id, p.Name, p.LegalEntityId, p.TotalUnits, mine.Count, occupied, vacant, mine.Count - occupied - vacant, mine.Count == 0 ? 0 : Math.Round(100m * occupied / mine.Count, 1),
                rent.GetValueOrDefault(p.Id), mine.Where(u => u.Status == UnitStatus.Vacant).Sum(u => u.AskingRent));
        }).ToList();
        var registered = rows.Sum(r => r.Registered); var occ = rows.Sum(r => r.Occupied);
        return new OccupancyDto(rows, registered == 0 ? 0 : Math.Round(100m * occ / registered, 1), rows.Sum(r => r.ContractedRent), rows.Sum(r => r.VacantAskingRent), rows.Sum(r => r.Vacant));
    }

    public async Task<PerformanceDto> Handle(GetPortfolioPerformance q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var props = await properties.Query(new Specification<Property>(p => p.IsActive && (entity == null || p.LegalEntityId == entity))).OrderBy(p => p.Name).Select(p => new { p.Id, p.Name, p.LegalEntityId, p.IndicativeValue }).ToListAsync(ct);
        var unitRows = await units.Query(new Specification<Unit>(u => entity == null || u.LegalEntityId == entity)).Select(u => new { u.PropertyId, u.Status }).ToListAsync(ct);

        // rent invoices -> instalment -> lease -> property
        var rentInvoices = await invoices.Query(new Specification<Invoice>(i => i.SourceType == BillingHandlers.SourceType && i.InvoiceDate >= q.From && i.InvoiceDate <= q.To && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Voided && (entity == null || i.LegalEntityId == entity)))
            .Select(i => new { i.Id, i.SourceId, i.IsCreditNote, i.Net }).ToListAsync(ct);
        var instalmentIds = rentInvoices.Where(i => i.SourceId != null).Select(i => i.SourceId!.Value).ToList();
        var instalmentLease = await instalments.Query(new Specification<RentInstalment>(i => instalmentIds.Contains(i.Id))).Select(i => new { i.Id, i.LeaseId }).ToDictionaryAsync(i => i.Id, i => i.LeaseId, ct);
        var leaseIds = instalmentLease.Values.Distinct().ToList();
        var leaseProperty = await leases.Query(new Specification<Lease>(l => leaseIds.Contains(l.Id))).Select(l => new { l.Id, l.PropertyId }).ToDictionaryAsync(l => l.Id, l => l.PropertyId, ct);
        Guid? PropertyOf(Guid? instalmentId) => instalmentId is { } id && instalmentLease.TryGetValue(id, out var lease) && leaseProperty.TryGetValue(lease, out var p) ? p : null;
        var invoiceProperty = rentInvoices.ToDictionary(i => i.Id, i => PropertyOf(i.SourceId));

        var invoiceIds = rentInvoices.Select(i => i.Id).ToList();
        var collected = (await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Date >= q.From && s.Date <= q.To))
            .SelectMany(s => s.Allocations, (s, a) => new { a.InvoiceId, a.Amount }).ToListAsync(ct))
            .Where(a => a.InvoiceId != null && invoiceProperty.ContainsKey(a.InvoiceId.Value)).ToList();

        var costs = (await workOrders.Query(new Specification<WorkOrder>(w => (w.Status == WorkOrderStatus.Completed || w.Status == WorkOrderStatus.Closed) && w.ActualCost != null && w.CompletedOn >= q.From && w.CompletedOn <= q.To && (entity == null || w.LegalEntityId == entity)))
            .Select(w => new { w.PropertyId, w.ActualCost }).ToListAsync(ct)).GroupBy(w => w.PropertyId).ToDictionary(g => g.Key, g => g.Sum(w => w.ActualCost ?? 0));

        var days = Math.Max(1, q.To.DayNumber - q.From.DayNumber + 1);
        PerformanceRow Build(Guid id, string name, Guid entityId, decimal value, decimal occupancy, decimal income, decimal opex, decimal billed, decimal collectedAmount)
        {
            var noi = income - opex;
            return new PerformanceRow(id, name, entityId, value, occupancy, income, opex, noi, value > 0 ? Math.Round(noi * 365m / days / value * 100m, 2) : 0, billed, collectedAmount, billed > 0 ? Math.Round(100m * collectedAmount / billed, 1) : 0);
        }
        var rows = props.Select(p =>
        {
            var mine = unitRows.Where(u => u.PropertyId == p.Id).ToList();
            var occupancy = mine.Count == 0 ? 0 : Math.Round(100m * mine.Count(u => u.Status == UnitStatus.Occupied) / mine.Count, 1);
            var billed = rentInvoices.Where(i => invoiceProperty[i.Id] == p.Id).Sum(i => i.IsCreditNote ? -i.Net : i.Net);
            var paid = collected.Where(a => invoiceProperty[a.InvoiceId!.Value] == p.Id).Sum(a => a.Amount);
            return Build(p.Id, p.Name, p.LegalEntityId, p.IndicativeValue, occupancy, billed, costs.GetValueOrDefault(p.Id), billed, paid);
        }).ToList();
        var all = unitRows.Count == 0 ? 0 : Math.Round(100m * unitRows.Count(u => u.Status == UnitStatus.Occupied) / unitRows.Count, 1);
        return new PerformanceDto(q.From, q.To, rows, Build(Guid.Empty, "Portfolio", Guid.Empty, rows.Sum(r => r.Value), all, rows.Sum(r => r.Income), rows.Sum(r => r.OperatingExpenses), rows.Sum(r => r.Billed), rows.Sum(r => r.Collected)));
    }

    public async Task<LeaseExpiriesDto> Handle(GetLeaseExpiries q, CancellationToken ct)
    {
        var entity = q.LegalEntityId; var today = DateOnly.FromDateTime(clock.UtcNow);
        var active = await leases.Query(new Specification<Lease>(l => l.Status == LeaseStatus.Active && (entity == null || l.LegalEntityId == entity))).Select(l => new { l.EndDate, l.AnnualRent }).ToListAsync(ct);
        var rows = new List<ExpiryRow>();
        var past = active.Where(l => l.EndDate < today).ToList();
        if (past.Count > 0) rows.Add(new("Past end date", null, past.Count, past.Sum(l => l.AnnualRent), true));
        var first = new DateOnly(today.Year, today.Month, 1);
        for (var i = 0; i < Math.Clamp(q.Months, 1, 36); i++)
        {
            var m = first.AddMonths(i);
            var inMonth = active.Where(l => l.EndDate >= today && l.EndDate.Year == m.Year && l.EndDate.Month == m.Month).ToList();
            rows.Add(new(m.ToString("MMM yyyy"), m, inMonth.Count, inMonth.Sum(l => l.AnnualRent), false));
        }
        return new LeaseExpiriesDto(rows, rows.Sum(r => r.Leases), rows.Sum(r => r.AnnualRentAtRisk));
    }

    public async Task<MaintenanceCostsDto> Handle(GetMaintenanceCosts q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var orders = await workOrders.Query(new Specification<WorkOrder>(w => w.ReportedOn >= q.From && w.ReportedOn <= q.To && (entity == null || w.LegalEntityId == entity)))
            .Select(w => new { w.PropertyId, w.Category, w.Status, w.EstimatedCost, w.ActualCost }).ToListAsync(ct);
        var propIds = orders.Select(o => o.PropertyId).Distinct().ToList();
        var names = await properties.Query(new Specification<Property>(p => propIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        bool IsOpen(WorkOrderStatus s) => s is not (WorkOrderStatus.Completed or WorkOrderStatus.Closed or WorkOrderStatus.Cancelled);
        var rows = orders.GroupBy(o => (o.PropertyId, Category: o.Category ?? "Uncategorised"))
            .Select(g => new MaintenanceRow(g.Key.PropertyId, names.GetValueOrDefault(g.Key.PropertyId, "?"), g.Key.Category, g.Count(), g.Count(o => IsOpen(o.Status)), g.Sum(o => o.EstimatedCost), g.Sum(o => o.ActualCost ?? 0)))
            .OrderBy(r => r.Property).ThenBy(r => r.Category).ToList();
        return new MaintenanceCostsDto(q.From, q.To, rows, rows.Sum(r => r.WorkOrders), rows.Sum(r => r.Open), rows.Sum(r => r.Estimated), rows.Sum(r => r.Actual));
    }

    public async Task<CommissionsDto> Handle(GetCommissionsReport q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var rows = await deals.Query(new Specification<BrokerageDeal>(d => d.DealDate >= q.From && d.DealDate <= q.To && d.Stage != DealStage.Lost && (entity == null || d.LegalEntityId == entity)))
            .Select(d => new { d.AgentPartyId, d.ExternalAgentName, d.DealValue, d.GrossCommission, d.AgentSharePercent, d.TeamLeadSharePercent, d.Stage }).ToListAsync(ct);
        var agentIds = rows.Where(r => r.AgentPartyId != null).Select(r => r.AgentPartyId!.Value).Distinct().ToList();
        var names = await parties.Query(new Specification<Party>(p => agentIds.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        string Agent(Guid? id, string? external) => id is { } k ? names.GetValueOrDefault(k, "?") : external ?? "Unassigned";
        decimal Share(decimal gross, decimal pct) => Math.Round(gross * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var result = rows.GroupBy(r => Agent(r.AgentPartyId, r.ExternalAgentName)).Select(g =>
        {
            var agent = g.Sum(r => Share(r.GrossCommission, r.AgentSharePercent)); var lead = g.Sum(r => Share(r.GrossCommission, r.TeamLeadSharePercent)); var gross = g.Sum(r => r.GrossCommission);
            return new CommissionRow(g.Key, g.Count(), g.Sum(r => r.DealValue), gross, agent, lead, gross - agent - lead, g.Where(r => r.Stage == DealStage.AgentPaid).Sum(r => Share(r.GrossCommission, r.AgentSharePercent)));
        }).OrderBy(r => r.Agent).ToList();
        var total = new CommissionRow("Total", result.Sum(r => r.Deals), result.Sum(r => r.DealValue), result.Sum(r => r.GrossCommission), result.Sum(r => r.AgentShare), result.Sum(r => r.TeamLeadShare), result.Sum(r => r.CompanyShare), result.Sum(r => r.PaidOut));
        return new CommissionsDto(q.From, q.To, result, total);
    }
}
