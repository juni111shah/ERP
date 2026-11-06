using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Organizations;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

/// <summary>Line arithmetic shared by sales and returns. Everything rounds to cents the way the server charges it.</summary>
public static class PosMath
{
    public readonly record struct Amounts(decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal Total);

    public static Amounts Line(decimal quantity, decimal unitPrice, decimal discountPercent, decimal taxRate)
    {
        var gross = Money.Round(quantity * unitPrice);
        var discount = Money.Round(gross * (discountPercent / 100m));
        var net = gross - discount;
        var tax = Money.Round(net * (taxRate / 100m));
        return new Amounts(gross, discount, net, tax, net + tax);
    }

    public static Amounts Line(PosOrderLine l) => Line(l.Quantity, l.UnitPrice, l.DiscountPercent, l.TaxRate);

    /// <summary>A debit and a credit on one account become one line, and lines that cancel out disappear, so the journal reads as what really moved.</summary>
    public static List<PostingLine> Consolidate(IEnumerable<PostingLine> lines)
    {
        var result = new List<PostingLine>();
        foreach (var g in lines.GroupBy(l => (l.AccountId, l.PartyId)))
        {
            var net = g.Sum(l => l.Debit - l.Credit);
            if (net == 0) continue;
            var description = g.First().Description;
            result.Add(net > 0 ? new PostingLine(g.Key.AccountId, net, 0, description, g.Key.PartyId) : new PostingLine(g.Key.AccountId, 0, -net, description, g.Key.PartyId));
        }
        return result;
    }
}

/// <summary>Which ledger account each kind of money at the till goes to: the register's own choice first, then the workspace's system roles.</summary>
public sealed class PosAccounts(IPostingService posting, IReadRepository<Item> items)
{
    private async Task<Guid> RoleOrFallbackAsync(string role, string fallback, CancellationToken ct)
    {
        try { return await posting.AccountForRoleAsync(role, ct); }
        catch (ConflictException) { return await posting.AccountForRoleAsync(fallback, ct); }
    }

    public async Task<Guid> CashAsync(PosRegister register, CancellationToken ct)
        => register.DefaultCashAccountId ?? await RoleOrFallbackAsync("pos.cash", "cash", ct);

    public async Task<Guid> CardAsync(PosRegister register, CancellationToken ct)
        => register.CardAccountId ?? await RoleOrFallbackAsync("pos.card-clearing", "bank", ct);

    /// <summary>Tenders that are neither cash nor card (a bank transfer, a voucher) wait in their own clearing account when there is one.</summary>
    public async Task<Guid> OtherAsync(PosRegister register, CancellationToken ct)
    {
        try { return await posting.AccountForRoleAsync("pos.other-clearing", ct); }
        catch (ConflictException) { return await CardAsync(register, ct); }
    }

    public Task<Guid> TenderAsync(PosRegister register, PosPaymentMethod method, CancellationToken ct) => method switch
    {
        PosPaymentMethod.Cash => CashAsync(register, ct),
        PosPaymentMethod.Card => CardAsync(register, ct),
        _ => OtherAsync(register, ct),
    };

    public async Task<Guid> VarianceAsync(PosRegister register, CancellationToken ct)
        => register.VarianceAccountId ?? await RoleOrFallbackAsync("pos.cash-variance", SystemRoles.PurchasesExpense, ct);

    public async Task<Guid> DrawerOffsetAsync(PosRegister register, CancellationToken ct)
        => register.ExpenseAccountId ?? await RoleOrFallbackAsync("pos.cash-misc", SystemRoles.PurchasesExpense, ct);

    public Task<Guid> SalesAsync(CancellationToken ct) => posting.AccountForRoleAsync("income.sales", ct);

    public Task<Guid> TaxAsync(CancellationToken ct) => posting.AccountForRoleAsync(SystemRoles.TaxOutput, ct);

    /// <summary>The income account per item: its own if set, else sales for goods and service income for services. Lines with no item go to sales.</summary>
    public async Task<Dictionary<Guid, Guid>> IncomeAccountsAsync(IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        var rows = await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Type, i.IncomeAccountId }).ToListAsync(ct);
        var result = new Dictionary<Guid, Guid>();
        Guid? sales = null, services = null;
        foreach (var r in rows)
        {
            if (r.IncomeAccountId is { } own) { result[r.Id] = own; continue; }
            if (r.Type == ItemType.Product) result[r.Id] = sales ??= await SalesAsync(ct);
            else result[r.Id] = services ??= await RoleOrFallbackAsync("income.services", "income.sales", ct);
        }
        return result;
    }
}

/// <summary>The shop's calendar: "today" and date ranges follow the workspace's time zone, not UTC.</summary>
public sealed class PosCalendar(IOrgContext org, IReadRepository<Organization> organizations, IClock clock)
{
    private TimeZoneInfo? zone;

    public async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct)
    {
        if (zone is not null) return zone;
        var id = org.OrgId;
        var name = id is null ? null : await organizations.Query(new Specification<Organization>(o => o.Id == id)).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        try { zone = string.IsNullOrWhiteSpace(name) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(name); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        return zone;
    }

    public async Task<DateOnly> TodayAsync(CancellationToken ct)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, await ZoneAsync(ct)));

    /// <summary>The UTC instants that bound these shop days: the start of <paramref name="from"/> up to, not including, the day after <paramref name="to"/>.</summary>
    public async Task<(DateTime Start, DateTime End)> RangeAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var z = await ZoneAsync(ct);
        return (ToUtc(from, z), ToUtc(to.AddDays(1), z));
    }

    private static DateTime ToUtc(DateOnly day, TimeZoneInfo z)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        // A day that starts inside a clock change has no such local time; move past the gap.
        while (z.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, z);
    }
}
