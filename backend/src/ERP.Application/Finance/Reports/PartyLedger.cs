namespace ERP.Application.Finance;

/// <summary>Running-balance arithmetic of a party statement (pure; unit-tested).</summary>
public static class PartyLedger
{
    public const int MaxDays = 3660;

    /// <summary>A document voided after its period was locked stays on the statement at its own date and is taken back on the date of the void, so a locked period's statement does not change.</summary>
    public const string VoidKind = "void";

    /// <summary>Kinds in the order they sort on the same day: charges first, then settlements.</summary>
    public static int Order(string kind) => kind switch { "invoice" or "bill" => 0, "creditNote" => 1, _ => 2 };

    public static PartyLedgerDto Build(Guid partyId, string? partyName, string side, DateOnly from, DateOnly to, decimal opening, IEnumerable<PartyLedgerLine> unsorted, bool debitRaises)
    {
        var balance = opening;
        var lines = new List<PartyLedgerLine>();
        foreach (var l in unsorted.OrderBy(l => l.Date).ThenBy(l => Order(l.Kind)).ThenBy(l => l.Number, StringComparer.Ordinal))
        {
            balance += debitRaises ? l.Debit - l.Credit : l.Credit - l.Debit;
            lines.Add(l with { Balance = balance });
        }
        return new PartyLedgerDto(partyId, partyName, side, from, to, opening, lines, lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), balance);
    }
}
