using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record AgeingRow(Guid PartyId, string? PartyName, decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);
public sealed record AgeingItem(Guid InvoiceId, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record ArAgeingDto(DateOnly AsOf, IReadOnlyList<AgeingRow> Rows, AgeingRow Totals);
public sealed record CustomerStatementDto(Guid PartyId, string? PartyName, DateOnly AsOf, IReadOnlyList<AgeingItem> Items, decimal UnallocatedReceipts, decimal Balance);

/// <summary>Receivables by customer, in buckets of days past due. Credit notes and unallocated receipts reduce the balance.</summary>
/// <summary>Optional filters (business unit, source type, party group) and grouping (<c>customer</c> default, <c>businessUnit</c>, <c>sourceType</c> or <c>partyGroup</c>). Receipts on account belong to a customer (so to its group) but carry no business unit or source: they appear unless a business unit or source filter is set, and only in customer or party-group rows.</summary>
public sealed record GetArAgeing(DateOnly AsOf, Guid? LegalEntityId, Guid? BusinessUnitId = null, string? SourceType = null, string? GroupBy = null, Guid? PartyGroupId = null) : IQuery<ArAgeingDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
public sealed record GetCustomerStatement(Guid PartyId, DateOnly AsOf, Guid? LegalEntityId) : IQuery<CustomerStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

internal sealed class ArReportHandlers(IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements, IReadRepository<ERP.Domain.Organizations.BusinessUnit> businessUnits, IReadRepository<Party> parties, IReadRepository<PartyGroup> partyGroups) :
    IRequestHandler<GetArAgeing, ArAgeingDto>,
    IRequestHandler<GetCustomerStatement, CustomerStatementDto>
{
    private sealed record Open(Guid PartyId, string? PartyName, Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid, Guid? BusinessUnitId = null, string? SourceType = null);

    public async Task<ArAgeingDto> Handle(GetArAgeing q, CancellationToken ct)
    {
        var mode = (q.GroupBy ?? "customer").ToLowerInvariant();
        if (mode is not ("customer" or "businessunit" or "sourcetype" or "partygroup")) throw new FieldValidationException("groupBy", "Group by customer, businessUnit, sourceType or partyGroup.");
        // Party -> group lookup (only what the report needs).
        Dictionary<Guid, Guid?>? groupOf = null;
        if (q.PartyGroupId is not null || mode == "partygroup")
        {
            var gid = q.PartyGroupId;
            groupOf = await parties.Query(new Specification<Party>(p => gid == null || p.PartyGroupId == gid)).Select(p => new { p.Id, p.PartyGroupId }).ToDictionaryAsync(p => p.Id, p => p.PartyGroupId, ct);
        }
        var groupParties = q.PartyGroupId is null ? null : groupOf!.Keys.ToHashSet();
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, null, ct, q.BusinessUnitId, q.SourceType, groupParties);
        var filtered = q.BusinessUnitId is not null || !string.IsNullOrWhiteSpace(q.SourceType);
        var rows = new Dictionary<Guid, AgeingRow>();
        if (mode is "customer" or "partygroup")
        {
            var groupNames = mode == "partygroup" ? await partyGroups.Query().Select(g => new { g.Id, g.Name }).ToDictionaryAsync(g => g.Id, g => g.Name, ct) : [];
            (Guid Key, string? Label) Bucket(Guid party, string? name)
            {
                if (mode == "customer") return (party, name);
                var g = groupOf!.GetValueOrDefault(party);
                return g is { } id ? (id, groupNames.GetValueOrDefault(id, "Unknown group")) : (Guid.Empty, "No group");
            }
            foreach (var o in open)
            {
                var (key, label) = Bucket(o.PartyId, o.PartyName);
                rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), q.AsOf, o.DueDate, Signed(o));
            }
            if (!filtered)
                foreach (var (party, name, amount) in await UnallocatedAsync(q.LegalEntityId, null, ct))
                {
                    if (groupParties is not null && !groupParties.Contains(party)) continue;
                    var (key, label) = Bucket(party, name);
                    rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), q.AsOf, q.AsOf, -amount);
                }
        }
        else
        {
            var unitNames = mode == "businessunit" ? await businessUnits.Query().Select(b => new { b.Id, b.Name }).ToDictionaryAsync(b => b.Id, b => b.Name, ct) : [];
            var labels = new Dictionary<Guid, string>();
            foreach (var o in open)
            {
                var (key, label) = mode == "businessunit"
                    ? (o.BusinessUnitId ?? Guid.Empty, o.BusinessUnitId is { } b ? unitNames.GetValueOrDefault(b, "Unknown unit") : "No business unit")
                    : (StableKey(o.SourceType), string.IsNullOrWhiteSpace(o.SourceType) ? "Direct invoices" : o.SourceType!);
                labels[key] = label;
                rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), q.AsOf, o.DueDate, Signed(o));
            }
        }
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(q.AsOf, list, totals);
    }

    private static Guid StableKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Guid.Empty;
        return new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant())));
    }

    public async Task<CustomerStatementDto> Handle(GetCustomerStatement q, CancellationToken ct)
    {
        var open = await OpenAsync(q.AsOf, q.LegalEntityId, q.PartyId, ct);
        var unallocated = (await UnallocatedAsync(q.LegalEntityId, q.PartyId, ct)).Sum(u => u.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, o.IsCreditNote, o.InvoiceDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new CustomerStatementDto(q.PartyId, open.FirstOrDefault()?.PartyName, q.AsOf, items, unallocated, open.Sum(Signed) - unallocated);
    }

    private static decimal Signed(Open o) => (o.IsCreditNote ? -1 : 1) * (o.Total - o.Paid);

    private async Task<List<Open>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct, Guid? businessUnitId = null, string? sourceType = null, IReadOnlyCollection<Guid>? onlyParties = null)
        => await invoices.Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.InvoiceDate <= asOf
                && (entityId == null || i.LegalEntityId == entityId) && (partyId == null || i.PartyId == partyId)
                && (businessUnitId == null || i.BusinessUnitId == businessUnitId) && (string.IsNullOrWhiteSpace(sourceType) || i.SourceType == sourceType)
                && (onlyParties == null || onlyParties.Contains(i.PartyId))))
            .Select(i => new Open(i.PartyId, i.PartyName, i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.Total, i.Paid, i.BusinessUnitId, i.SourceType)).ToListAsync(ct);

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
