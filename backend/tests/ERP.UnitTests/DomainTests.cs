using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Domain.Finance;
using ERP.Domain.Platform;

namespace ERP.UnitTests;

public class DomainTests
{
    private static JournalEntry Journal(params (decimal Dr, decimal Cr)[] lines)
    {
        var j = new JournalEntry { Number = "JV-1", Date = new DateOnly(2026, 10, 22), CurrencyCode = "USD" };
        j.Lines.AddRange(lines.Select((l, i) => new JournalLine { Seq = i + 1, AccountId = Guid.NewGuid(), Debit = l.Dr, Credit = l.Cr }));
        return j;
    }

    [Fact]
    public void Balanced_journal_posts()
    {
        var j = Journal((100, 0), (0, 100));
        j.Post(Guid.NewGuid(), DateTime.UtcNow, null);
        Assert.Equal(JournalStatus.Posted, j.Status);
    }

    [Fact]
    public void Unbalanced_or_two_sided_or_locked_journals_are_rejected()
    {
        Assert.Throws<DomainException>(() => Journal((100, 0), (0, 90)).EnsureCanPost(null));
        Assert.Throws<DomainException>(() => Journal((100, 100), (0, 0)).EnsureCanPost(null));
        Assert.Throws<DomainException>(() => Journal((100, 0)).EnsureCanPost(null));
        Assert.Throws<DomainException>(() => Journal((100, 0), (0, 100)).EnsureCanPost(new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Maker_cannot_approve_own_request()
    {
        var maker = Guid.NewGuid();
        var req = new ApprovalRequest { RequestedBy = maker, Subject = "x", DocumentType = "general" };
        Assert.Throws<DomainException>(() => req.Decide(maker, true, null, DateTime.UtcNow));
        req.Decide(Guid.NewGuid(), true, "ok", DateTime.UtcNow);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
        Assert.Throws<DomainException>(() => req.Decide(Guid.NewGuid(), false, null, DateTime.UtcNow));
    }

    [Theory]
    [InlineData("{prefix}-{yyyy}-{seq:6}", "INV", 1, "INV-2026-000001")]
    [InlineData("{prefix}-{seq:4}", "PTY", 42, "PTY-0042")]
    [InlineData("{prefix}/{yy}{mm}/{seq}", "JV", 7, "JV/2610/7")]
    public void Number_formats(string pattern, string prefix, long value, string expected)
        => Assert.Equal(expected, NumberFormatter.Format(pattern, prefix, new DateOnly(2026, 10, 22), value));
}
