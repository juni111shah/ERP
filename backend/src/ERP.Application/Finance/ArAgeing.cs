using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record AgeingRow(Guid PartyId, string? PartyName, decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);
public sealed record AgeingItem(Guid InvoiceId, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record ArAgeingDto(DateOnly AsOf, IReadOnlyList<AgeingRow> Rows, AgeingRow Totals);
public sealed record CustomerStatementDto(Guid PartyId, string? PartyName, DateOnly AsOf, IReadOnlyList<AgeingItem> Items, decimal UnallocatedReceipts, decimal Balance);

/// <summary>Receivables by customer, in buckets of days past due. Credit notes and unallocated receipts reduce the balance.</summary>
public sealed record GetArAgeing(DateOnly AsOf, Guid? LegalEntityId) : IQuery<ArAgeingDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetCustomerStatement(Guid PartyId, DateOnly AsOf, Guid? LegalEntityId) : IQuery<CustomerStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

internal sealed class ArReportHandlers(IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements) :
    IRequestHandler<GetArAgeing, ArAgeingDto>,
    IRequestHandler<GetCustomerStatement, CustomerStatementDto>
{
    private sealed record Open(Guid PartyId, string? PartyName, Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid);

    public async Task<ArAgeingDto> Handle(GetArAgeing q, CancellationToken ct)
    {
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, null, ct);
        var onAccount = await UnallocatedAsync(q.LegalEntityId, null, ct);
        var rows = new Dictionary<Guid, AgeingRow>();
        foreach (var o in open) rows[o.PartyId] = Ageing.Add(rows.GetValueOrDefault(o.PartyId) ?? new AgeingRow(o.PartyId, o.PartyName, 0, 0, 0, 0, 0, 0), q.AsOf, o.DueDate, Signed(o));
        foreach (var (party, name, amount) in onAccount) rows[party] = Ageing.Add(rows.GetValueOrDefault(party) ?? new AgeingRow(party, name, 0, 0, 0, 0, 0, 0), q.AsOf, q.AsOf, -amount);
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(q.AsOf, list, totals);
    }

    public async Task<CustomerStatementDto> Handle(GetCustomerStatement q, CancellationToken ct)
    {
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, q.PartyId, ct);
        var unallocated = (await UnallocatedAsync(q.LegalEntityId, q.PartyId, ct)).Sum(u => u.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, o.IsCreditNote, o.InvoiceDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new CustomerStatementDto(q.PartyId, open.FirstOrDefault()?.PartyName, q.AsOf, items, unallocated, open.Sum(Signed) - unallocated);
    }

    private static decimal Signed(Open o) => (o.IsCreditNote ? -1 : 1) * (o.Total - o.Paid);

    private async Task<List<Open>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.InvoiceDate <= asOf
                && (entityId == null || i.LegalEntityId == entityId) && (partyId == null || i.PartyId == partyId)))
            .Select(i => new Open(i.PartyId, i.PartyName, i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.Total, i.Paid)).ToListAsync(ct);

    private async Task<List<(Guid Party, string? Name, decimal Amount)>> UnallocatedAsync(Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var rows = await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Unallocated > 0
                && (entityId == null || s.LegalEntityId == entityId) && (partyId == null || s.PartyId == partyId)))
            .Select(s => new { s.PartyId, s.PartyName, s.Unallocated }).ToListAsync(ct);
        return rows.GroupBy(r => r.PartyId).Select(g => (g.Key, g.First().PartyName, g.Sum(r => r.Unallocated))).ToList();
    }
}

/// <summary>Buckets an amount by days past due (shared by receivables and payables ageing).</summary>
internal static class Ageing
{
    public static AgeingRow Add(AgeingRow r, DateOnly asOf, DateOnly due, decimal amount)
    {
        var late = asOf.DayNumber - due.DayNumber;
        return late switch
        {
            <= 0 => r with { Current = r.Current + amount, Total = r.Total + amount },
            <= 30 => r with { Days1To30 = r.Days1To30 + amount, Total = r.Total + amount },
            <= 60 => r with { Days31To60 = r.Days31To60 + amount, Total = r.Total + amount },
            <= 90 => r with { Days61To90 = r.Days61To90 + amount, Total = r.Total + amount },
            _ => r with { Over90 = r.Over90 + amount, Total = r.Total + amount },
        };
    }
}
