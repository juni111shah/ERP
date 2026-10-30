using ERP.Application.Finance;

namespace ERP.UnitTests;

public class PartyLedgerTests
{
    private static PartyLedgerLine L(int day, string kind, string number, decimal debit, decimal credit) =>
        new(new DateOnly(2026, 1, day), kind, Guid.NewGuid(), number, null, "AED", debit, credit, 0);

    [Fact]
    public void Customer_balance_runs_from_the_opening_balance_and_debits_raise_it()
    {
        var dto = PartyLedger.Build(Guid.NewGuid(), "C", "customer", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 100m,
            [L(5, "receipt", "RC-1", 0, 300), L(3, "invoice", "INV-1", 500, 0), L(5, "creditNote", "CN-1", 0, 50), L(5, "invoice", "INV-2", 200, 0)],
            debitRaises: true);

        Assert.Equal(["INV-1", "INV-2", "CN-1", "RC-1"], dto.Lines.Select(l => l.Number));
        Assert.Equal([600m, 800m, 750m, 450m], dto.Lines.Select(l => l.Balance));
        Assert.Equal(700m, dto.TotalDebit);
        Assert.Equal(350m, dto.TotalCredit);
        Assert.Equal(dto.OpeningBalance + dto.TotalDebit - dto.TotalCredit, dto.ClosingBalance);
    }

    [Fact]
    public void Vendor_balance_is_payable_so_credits_raise_it()
    {
        var dto = PartyLedger.Build(Guid.NewGuid(), "V", "vendor", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 0m,
            [L(2, "bill", "B-1", 0, 1000), L(9, "payment", "PY-1", 400, 0)], debitRaises: false);

        Assert.Equal([1000m, 600m], dto.Lines.Select(l => l.Balance));
        Assert.Equal(600m, dto.ClosingBalance);
    }

    [Fact]
    public void Empty_range_keeps_the_opening_balance()
    {
        var dto = PartyLedger.Build(Guid.NewGuid(), null, "customer", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 42m, [], debitRaises: true);
        Assert.Empty(dto.Lines);
        Assert.Equal(42m, dto.ClosingBalance);
    }
}
