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

/// <summary>A reduction of what is owed to a vendor that is not a payment. With no bill it is credit held against the vendor.</summary>
public sealed record BillAdjustment(Guid PartyId, string? PartyName, Guid? BillId, decimal Amount);

/// <summary>Lets a module report what it took off payables, such as a purchase return, so ageing agrees with the ledger.</summary>
public interface IBillAdjustmentSource
{
    Task<IReadOnlyList<BillAdjustment>> AsOfAsync(DateOnly asOf, Guid? legalEntityId, Guid? partyId, CancellationToken ct);
}

internal sealed class ApReportHandlers(IReadRepository<Bill> bills, IReadRepository<Settlement> settlements, IReportCurrencyGuard currencies, IEnumerable<IBillAdjustmentSource> adjustmentSources) :
    IRequestHandler<GetApAgeing, ArAgeingDto>,
    IRequestHandler<GetVendorStatement, VendorStatementDto>
{
    private sealed record Open(Guid PartyId, string? PartyName, Guid Id, string Number, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid);

    public async Task<ArAgeingDto> Handle(GetApAgeing q, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, null, ct);
        var rows = new Dictionary<Guid, AgeingRow>();
        foreach (var o in open) rows[o.PartyId] = Ageing.Add(rows.GetValueOrDefault(o.PartyId) ?? new AgeingRow(o.PartyId, o.PartyName, 0, 0, 0, 0, 0, 0), q.AsOf, o.DueDate, o.Total - o.Paid);
        foreach (var (party, name, amount) in await AdvancesAsync(q.AsOf, q.LegalEntityId, null, ct)) rows[party] = Ageing.Add(rows.GetValueOrDefault(party) ?? new AgeingRow(party, name, 0, 0, 0, 0, 0, 0), q.AsOf, q.AsOf, -amount);
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(q.AsOf, list, totals);
    }

    public async Task<VendorStatementDto> Handle(GetVendorStatement q, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, q.PartyId, ct);
        var advances = (await AdvancesAsync(q.AsOf, q.LegalEntityId, q.PartyId, ct)).Sum(a => a.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, false, o.BillDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new VendorStatementDto(q.PartyId, open.FirstOrDefault()?.PartyName, q.AsOf, items, advances, open.Sum(o => o.Total - o.Paid) - advances);
    }

    /// <summary>Bills open at the end of <paramref name="asOf"/>: later payments and later voids do not change the picture. An allocation counts from the date of its payment.</summary>
    private async Task<List<Open>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var cutoff = CutOff(asOf);
        var rows = await bills.Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid || b.Status == DocumentStatus.Voided)
                && b.BillDate <= asOf && !(b.VoidedAt != null && b.VoidedAt < cutoff)
                && (entityId == null || b.LegalEntityId == entityId) && (partyId == null || b.PartyId == partyId)))
            .Select(b => new Open(b.PartyId, b.PartyName, b.Id, b.Number, b.BillDate, b.DueDate, b.Total, 0m)).ToListAsync(ct);
        if (rows.Count == 0) return rows;
        var paid = (await Issued(asOf, entityId, partyId).SelectMany(s => s.Allocations).Select(a => new { a.BillId, a.Amount }).ToListAsync(ct))
            .Where(a => a.BillId != null).GroupBy(a => a.BillId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        var adjusted = (await AdjustmentsAsync(asOf, entityId, partyId, ct)).Where(a => a.BillId != null).GroupBy(a => a.BillId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        return rows.Select(o => o with { Paid = paid.GetValueOrDefault(o.Id) + adjusted.GetValueOrDefault(o.Id) }).Where(o => o.Total - o.Paid > 0).ToList();
    }

    private async Task<List<(Guid Party, string? Name, decimal Amount)>> AdvancesAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var payments = await Issued(asOf, entityId, partyId).Select(s => new { s.Id, s.PartyId, s.PartyName, s.Amount }).ToListAsync(ct);
        var allocated = (await Issued(asOf, entityId, partyId).SelectMany(s => s.Allocations).Select(a => new { a.SettlementId, a.Amount }).ToListAsync(ct))
            .GroupBy(a => a.SettlementId).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        var open = payments.Select(p => (p.PartyId, p.PartyName, Open: p.Amount - allocated.GetValueOrDefault(p.Id))).Where(p => p.Open > 0).ToList();
        open.AddRange((await AdjustmentsAsync(asOf, entityId, partyId, ct)).Where(a => a.BillId == null).Select(a => (a.PartyId, a.PartyName, a.Amount)));
        return open.GroupBy(p => p.PartyId).Select(g => (g.Key, g.First().PartyName, g.Sum(p => p.Item3))).ToList();
    }

    private async Task<List<BillAdjustment>> AdjustmentsAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var all = new List<BillAdjustment>();
        foreach (var source in adjustmentSources) all.AddRange(await source.AsOfAsync(asOf, entityId, partyId, ct));
        return all;
    }

    /// <summary>Payments dated on or before the date that were not voided by the end of that day.</summary>
    private IQueryable<Settlement> Issued(DateOnly asOf, Guid? entityId, Guid? partyId)
    {
        var cutoff = CutOff(asOf);
        return settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Issued && s.Date <= asOf && !(s.VoidedAt != null && s.VoidedAt < cutoff)
            && (s.Status == DocumentStatus.Issued || s.Status == DocumentStatus.Voided)
            && (entityId == null || s.LegalEntityId == entityId) && (partyId == null || s.PartyId == partyId)));
    }

    private static DateTime CutOff(DateOnly asOf) => asOf.AddDays(1).ToDateTime(TimeOnly.MinValue);
}
