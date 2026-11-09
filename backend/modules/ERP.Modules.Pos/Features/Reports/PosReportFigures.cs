using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

/// <summary>The tables shared by the shift report and the sales report: tenders, groups and top items, worked out from the loaded documents.</summary>
internal static class PosReportFigures
{
    private const int TopItemCount = 10;

    public static List<PosTenderRowDto> Tenders(IEnumerable<PosOrder> sales, IEnumerable<PosReturn> given)
    {
        var paid = sales.SelectMany(o => o.Payments).GroupBy(p => p.Method).ToDictionary(g => g.Key, g => (Amount: g.Sum(p => p.Amount), Count: g.Count()));
        var back = given.SelectMany(r => r.Refunds).GroupBy(f => f.Method).ToDictionary(g => g.Key, g => g.Sum(f => f.Amount));
        return paid.Keys.Union(back.Keys).OrderBy(m => m).Select(m =>
        {
            var p = paid.GetValueOrDefault(m);
            var b = back.GetValueOrDefault(m);
            return new PosTenderRowDto(m, p.Amount, b, p.Amount - b, p.Count);
        }).ToList();
    }

    public static List<PosSalesGroupDto> Groups(IEnumerable<PosOrder> sales, IEnumerable<PosReturn> given, Func<PosOrder, string> saleKey, Func<PosReturn, string> returnKey)
    {
        var sold = sales.GroupBy(saleKey).ToDictionary(g => g.Key, g => (Amount: g.Sum(o => o.TotalAmount), Count: g.Count()));
        var back = given.GroupBy(returnKey).ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));
        return sold.Keys.Union(back.Keys).Select(k =>
        {
            var s = sold.GetValueOrDefault(k);
            var b = back.GetValueOrDefault(k);
            return new PosSalesGroupDto(k, s.Amount, b, s.Amount - b, s.Count);
        }).OrderByDescending(g => g.Net).ToList();
    }

    public static List<PosTopItemDto> TopItems(IEnumerable<PosOrder> sales) => sales.SelectMany(o => o.Lines)
        .GroupBy(l => (l.ItemCode, l.Description))
        .Select(g => new PosTopItemDto(g.Key.ItemCode, g.Key.Description, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
        .OrderByDescending(i => i.Revenue).Take(TopItemCount).ToList();
}
