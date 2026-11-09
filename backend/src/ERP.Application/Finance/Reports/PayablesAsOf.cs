using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>What is owed to vendors at the end of a date; shared by the payables ageing and the vendor statement.</summary>
public sealed class PayablesAsOf(IBillRepository bills, ISettlementRepository settlements, IEnumerable<IBillAdjustmentSource> adjustmentSources)
{
    /// <summary>Bills open at the end of <paramref name="asOf"/>: later payments and later voids do not change the picture. An allocation counts from the date of its payment.</summary>
    public async Task<List<PayableOpenItem>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var rows = await bills.ListPostedAsOfAsync(asOf, entityId, partyId, ct);
        if (rows.Count == 0) return rows;
        var paid = await settlements.AllocatedPerBillAsOfAsync(asOf, entityId, partyId, ct);
        var adjusted = (await AdjustmentsAsync(asOf, entityId, partyId, ct)).Where(a => a.BillId != null).GroupBy(a => a.BillId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        return rows.Select(o => o with { Paid = paid.GetValueOrDefault(o.Id) + adjusted.GetValueOrDefault(o.Id) }).Where(o => o.Total - o.Paid > 0).ToList();
    }

    /// <summary>What is left unallocated on the payments of each vendor (vendor advances) at the end of <paramref name="asOf"/>, plus the credit held against the vendor.</summary>
    public async Task<List<(Guid Party, string? Name, decimal Amount)>> AdvancesAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var payments = await settlements.ListNotVoidedAsOfAsync(InstrumentDirection.Issued, asOf, entityId, partyId, ct);
        var allocated = await settlements.AllocatedPerSettlementAsOfAsync(InstrumentDirection.Issued, asOf, entityId, partyId, ct);
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
}
