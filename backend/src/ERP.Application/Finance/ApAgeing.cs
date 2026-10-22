using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record VendorStatementDto(Guid PartyId, string? PartyName, DateOnly AsOf, IReadOnlyList<AgeingItem> Items, decimal UnallocatedPayments, decimal Balance);

/// <summary>Payables by vendor in buckets of days past due. Unallocated payments (vendor advances) reduce what is owed.</summary>
public sealed record GetApAgeing(DateOnly AsOf, Guid? LegalEntityId) : IQuery<ArAgeingDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetVendorStatement(Guid PartyId, DateOnly AsOf, Guid? LegalEntityId) : IQuery<VendorStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

internal sealed class ApReportHandlers(IReadRepository<Bill> bills, IReadRepository<Settlement> settlements) :
    IRequestHandler<GetApAgeing, ArAgeingDto>,
    IRequestHandler<GetVendorStatement, VendorStatementDto>
{
    private sealed record Open(Guid PartyId, string? PartyName, Guid Id, string Number, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid);

    public async Task<ArAgeingDto> Handle(GetApAgeing q, CancellationToken ct)
    {
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, null, ct);
        var rows = new Dictionary<Guid, AgeingRow>();
        foreach (var o in open) rows[o.PartyId] = Ageing.Add(rows.GetValueOrDefault(o.PartyId) ?? new AgeingRow(o.PartyId, o.PartyName, 0, 0, 0, 0, 0, 0), q.AsOf, o.DueDate, o.Total - o.Paid);
        foreach (var (party, name, amount) in await AdvancesAsync(q.LegalEntityId, null, ct)) rows[party] = Ageing.Add(rows.GetValueOrDefault(party) ?? new AgeingRow(party, name, 0, 0, 0, 0, 0, 0), q.AsOf, q.AsOf, -amount);
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(q.AsOf, list, totals);
    }

    public async Task<VendorStatementDto> Handle(GetVendorStatement q, CancellationToken ct)
    {
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, q.PartyId, ct);
        var advances = (await AdvancesAsync(q.LegalEntityId, q.PartyId, ct)).Sum(a => a.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, false, o.BillDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new VendorStatementDto(q.PartyId, open.FirstOrDefault()?.PartyName, q.AsOf, items, advances, open.Sum(o => o.Total - o.Paid) - advances);
    }

    private async Task<List<Open>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.BillDate <= asOf
                && (entityId == null || b.LegalEntityId == entityId) && (partyId == null || b.PartyId == partyId)))
            .Select(b => new Open(b.PartyId, b.PartyName, b.Id, b.Number, b.BillDate, b.DueDate, b.Total, b.Paid)).ToListAsync(ct);

    private async Task<List<(Guid Party, string? Name, decimal Amount)>> AdvancesAsync(Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var rows = await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Issued && s.Status == DocumentStatus.Issued && s.Unallocated > 0
                && (entityId == null || s.LegalEntityId == entityId) && (partyId == null || s.PartyId == partyId)))
            .Select(s => new { s.PartyId, s.PartyName, s.Unallocated }).ToListAsync(ct);
        return rows.GroupBy(r => r.PartyId).Select(g => (g.Key, g.First().PartyName, g.Sum(r => r.Unallocated))).ToList();
    }
}
