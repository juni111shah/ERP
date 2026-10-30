using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

public sealed record OwnerStatementRow(Guid PropertyId, string Property, decimal FeePercent, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner, decimal ChargedToTenant = 0);
/// <summary>One maintenance job in the period: what it cost, where the cost figure came from (bill, actual, estimate) and who bears it.</summary>
public sealed record OwnerStatementLine(Guid WorkOrderId, string? Number, string Property, string Issue, DateOnly? CompletedOn, decimal Cost, string CostSource, string BorneBy, decimal OwnerAmount, decimal TenantAmount);
public sealed record OwnerStatementDto(Guid OwnerPartyId, string Owner, DateOnly From, DateOnly To, IReadOnlyList<OwnerStatementRow> Rows, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner,
    IReadOnlyList<OwnerStatementLine>? Lines = null, decimal ChargedToTenant = 0);

/// <summary>
/// What a property owner is due for a period: rent actually collected (receipts allocated to the rent invoices of their properties, by receipt date),
/// less the management fee on it and the owner's share of the maintenance cost of work completed in the period (cost from the linked vendor bill when there is one, otherwise the actual or estimated cost; each job can be charged to the owner, the company, the tenant or shared). Cash basis.
/// </summary>
public sealed record GetOwnerStatement(Guid OwnerPartyId, DateOnly From, DateOnly To) : IQuery<OwnerStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

public sealed class GetOwnerStatementValidator : AbstractValidator<GetOwnerStatement>
{
    public GetOwnerStatementValidator() { RuleFor(x => x.OwnerPartyId).NotEmpty(); RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From); }
}

internal sealed class OwnerStatementHandler(IReadRepository<Property> properties, IReadRepository<Party> parties, IReadRepository<Lease> leases, IReadRepository<RentInstalment> instalments,
    IReadRepository<Settlement> settlements, IReadRepository<WorkOrder> workOrders, IReadRepository<Bill> bills) : IRequestHandler<GetOwnerStatement, OwnerStatementDto>
{
    public async Task<OwnerStatementDto> Handle(GetOwnerStatement q, CancellationToken ct)
    {
        var owner = await parties.GetAsync(q.OwnerPartyId, ct);
        var props = await properties.Query(new Specification<Property>(p => p.OwnerPartyId == q.OwnerPartyId && p.OwnershipType == OwnershipType.ManagedForOwner)).OrderBy(p => p.Name).ToListAsync(ct);
        var propIds = props.Select(p => p.Id).ToList();
        var leaseProp = await leases.Query(new Specification<Lease>(l => propIds.Contains(l.PropertyId))).Select(l => new { l.Id, l.PropertyId }).ToDictionaryAsync(l => l.Id, l => l.PropertyId, ct);
        var leaseIds = leaseProp.Keys.ToList();
        var instalmentInvoice = await instalments.Query(new Specification<RentInstalment>(i => leaseIds.Contains(i.LeaseId) && i.InvoiceId != null))
            .Select(i => new { i.LeaseId, InvoiceId = i.InvoiceId!.Value }).ToListAsync(ct);
        var invoiceProperty = instalmentInvoice.ToDictionary(i => i.InvoiceId, i => leaseProp[i.LeaseId]);
        var invoiceIds = invoiceProperty.Keys.ToList();

        // Receipts in the period, with the part of each that went to the owner's rent invoices (summed on the client: SQLite cannot add decimals).
        var allocations = await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Date >= q.From && s.Date <= q.To))
            .SelectMany(s => s.Allocations, (s, a) => new { a.InvoiceId, a.Amount }).ToListAsync(ct);
        var collected = allocations.Where(a => a.InvoiceId is { } id && invoiceProperty.ContainsKey(id)).GroupBy(a => invoiceProperty[a.InvoiceId!.Value]).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var orders = await workOrders.Query(new Specification<WorkOrder>(w => propIds.Contains(w.PropertyId) && (w.Status == WorkOrderStatus.Completed || w.Status == WorkOrderStatus.Closed) && w.CompletedOn >= q.From && w.CompletedOn <= q.To))
            .Select(w => new { w.Id, w.Number, w.Issue, w.PropertyId, w.CompletedOn, Basis = w.ActualCost ?? w.EstimatedCost, HasActual = w.ActualCost != null, w.ChargeableTo, w.OwnerSharePercent, w.BillId }).ToListAsync(ct);

        // A linked bill pays for all the work orders linked to it (any period): its net amount is split by their own cost figures (equally when they are all zero).
        var billIds = orders.Where(o => o.BillId != null).Select(o => o.BillId!.Value).Distinct().ToList();
        var billNet = billIds.Count == 0 ? [] : await bills.Query(new Specification<Bill>(b => billIds.Contains(b.Id))).Select(b => new { b.Id, b.Net }).ToDictionaryAsync(b => b.Id, b => b.Net, ct);
        var siblings = billIds.Count == 0 ? [] : (await workOrders.Query(new Specification<WorkOrder>(w => w.BillId != null && billIds.Contains(w.BillId.Value)))
            .Select(w => new { w.Id, BillId = w.BillId!.Value, Basis = w.ActualCost ?? w.EstimatedCost }).ToListAsync(ct)).GroupBy(w => w.BillId).ToDictionary(g => g.Key, g => g.ToList());
        var propName = props.ToDictionary(p => p.Id, p => p.Name);

        var lines = new List<OwnerStatementLine>();
        var ownerCost = new List<(Guid PropertyId, OwnerStatementLine Line)>();
        foreach (var o in orders.OrderBy(o => o.CompletedOn).ThenBy(o => o.Number))
        {
            decimal cost; string source;
            if (o.BillId is { } bid && billNet.TryGetValue(bid, out var net) && siblings.TryGetValue(bid, out var group))
            {
                var total = group.Sum(g => g.Basis);
                cost = Math.Round(total > 0 ? net * o.Basis / total : net / group.Count, 2);
                source = "bill";
            }
            else { cost = o.Basis; source = o.HasActual ? "actual" : "estimate"; }
            var ownerShare = o.ChargeableTo switch { CostResponsibility.Company or CostResponsibility.Tenant => 0m, CostResponsibility.Shared => o.OwnerSharePercent, _ => 100m };
            var ownerAmount = Math.Round(cost * ownerShare / 100m, 2);
            var tenantAmount = o.ChargeableTo == CostResponsibility.Tenant ? cost : 0m;
            var borneBy = o.ChargeableTo switch { CostResponsibility.Company => "Company", CostResponsibility.Tenant => "Tenant", CostResponsibility.Shared => $"Shared ({ownerShare:0.##}% owner)", _ => "Owner" };
            var line = new OwnerStatementLine(o.Id, o.Number, propName[o.PropertyId], o.Issue, o.CompletedOn, cost, source, borneBy, ownerAmount, tenantAmount);
            lines.Add(line);
            ownerCost.Add((o.PropertyId, line));
        }

        var rows = props.Select(p =>
        {
            var rent = collected.GetValueOrDefault(p.Id);
            var fee = Math.Round(rent * p.ManagementFeePercent / 100m, 2);
            var cost = ownerCost.Where(x => x.PropertyId == p.Id).Sum(x => x.Line.OwnerAmount);
            var tenant = ownerCost.Where(x => x.PropertyId == p.Id).Sum(x => x.Line.TenantAmount);
            return new OwnerStatementRow(p.Id, p.Name, p.ManagementFeePercent, rent, fee, cost, rent - fee - cost, tenant);
        }).ToList();
        return new(owner.Id, owner.Name, q.From, q.To, rows, rows.Sum(r => r.RentCollected), rows.Sum(r => r.ManagementFee), rows.Sum(r => r.MaintenanceCosts), rows.Sum(r => r.NetToOwner), lines, rows.Sum(r => r.ChargedToTenant));
    }
}
