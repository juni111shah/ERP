namespace ERP.Application.Finance;

/// <summary>Splits the net cash of one journal over the entries on the other side of it (pure).</summary>
public static class CashFlowAllocator
{
    public sealed record EntryLine(Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>
    /// Positive amounts are inflows. Money in (net debit to cash) comes from the non-cash credits and money out goes to the non-cash debits, in proportion, to the cent;
    /// the last line takes the rounding so the parts always add up to the cash. Lines on the same side as the cash (the accumulated depreciation
    /// written off in a disposal, say) moved no cash. A journal that only moves money between cash accounts allocates nothing.
    /// </summary>
    public static IReadOnlyList<(Guid AccountId, decimal Amount)> Allocate(IReadOnlyCollection<EntryLine> lines, IReadOnlySet<Guid> cashAccounts)
    {
        var net = lines.Where(l => cashAccounts.Contains(l.AccountId)).Sum(l => l.Debit - l.Credit);
        if (net == 0) return [];
        var other = lines.Where(l => !cashAccounts.Contains(l.AccountId))
            .Select(l => (l.AccountId, Amount: net > 0 ? l.Credit : l.Debit)).Where(l => l.Amount > 0).ToList();
        var total = other.Sum(l => l.Amount);
        var result = new List<(Guid, decimal)>();
        var left = Math.Abs(net);
        for (var i = 0; i < other.Count; i++)
        {
            var share = i == other.Count - 1 ? left : Math.Round(Math.Abs(net) * other[i].Amount / total, 2, MidpointRounding.AwayFromZero);
            left -= share;
            result.Add((other[i].AccountId, net > 0 ? share : -share));
        }
        return result;
    }
}
