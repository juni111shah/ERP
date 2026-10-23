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

public sealed record OwnerStatementRow(Guid PropertyId, string Property, decimal FeePercent, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner);
public sealed record OwnerStatementDto(Guid OwnerPartyId, string Owner, DateOnly From, DateOnly To, IReadOnlyList<OwnerStatementRow> Rows, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner);

/// <summary>
/// What a property owner is due for a period: rent actually collected (receipts allocated to the rent invoices of their properties, by receipt date),
/// less the management fee on it and the maintenance cost of work completed in the period. Cash basis.
/// </summary>
public sealed record GetOwnerStatement(Guid OwnerPartyId, DateOnly From, DateOnly To) : IQuery<OwnerStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

public sealed class GetOwnerStatementValidator : AbstractValidator<GetOwnerStatement>
{
    public GetOwnerStatementValidator() { RuleFor(x => x.OwnerPartyId).NotEmpty(); RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From); }
}

internal sealed class OwnerStatementHandler(IReadRepository<Property> properties, IReadRepository<Party> parties, IReadRepository<Lease> leases, IReadRepository<RentInstalment> instalments,
    IReadRepository<Settlement> settlements, IReadRepository<WorkOrder> workOrders) : IRequestHandler<GetOwnerStatement, OwnerStatementDto>
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
            .Select(w => new { w.PropertyId, Cost = w.ActualCost ?? w.EstimatedCost }).ToListAsync(ct);
        var maintenance = orders.GroupBy(o => o.PropertyId).ToDictionary(g => g.Key, g => g.Sum(o => o.Cost));

        var rows = props.Select(p =>
        {
            var rent = collected.GetValueOrDefault(p.Id);
            var fee = Math.Round(rent * p.ManagementFeePercent / 100m, 2);
            var cost = maintenance.GetValueOrDefault(p.Id);
            return new OwnerStatementRow(p.Id, p.Name, p.ManagementFeePercent, rent, fee, cost, rent - fee - cost);
        }).ToList();
        return new(owner.Id, owner.Name, q.From, q.To, rows, rows.Sum(r => r.RentCollected), rows.Sum(r => r.ManagementFee), rows.Sum(r => r.MaintenanceCosts), rows.Sum(r => r.NetToOwner));
    }
}
