using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Parties;

namespace ERP.Application.Finance;

internal sealed class GetArAgeingHandler(
    ReceivablesAsOf receivables,
    IPartyRepository parties,
    IPartyGroupRepository partyGroups,
    IBusinessUnitRepository businessUnits,
    IReportCurrencyGuard currencies)
    : IQueryHandler<GetArAgeingQuery, ArAgeingDto>
{
    public async Task<ArAgeingDto> Handle(GetArAgeingQuery query, CancellationToken ct)
    {
        // 1. One currency only, and a known grouping.
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var mode = (query.GroupBy ?? "customer").ToLowerInvariant();
        if (mode is not ("customer" or "businessunit" or "sourcetype" or "partygroup")) throw new FieldValidationException("groupBy", "Group by customer, businessUnit, sourceType or partyGroup.");

        // 2. Party -> group lookup (only what the report needs), then the open documents.
        Dictionary<Guid, Guid?>? groupOf = null;
        if (query.PartyGroupId is not null || mode == "partygroup")
            groupOf = await parties.GroupOfAsync(query.PartyGroupId, ct);
        var groupParties = query.PartyGroupId is null ? null : groupOf!.Keys.ToHashSet();
        var open = await receivables.OpenAsync(query.AsOf, query.LegalEntityId, null, ct, query.BusinessUnitId, query.SourceType, groupParties);
        var filtered = query.BusinessUnitId is not null || !string.IsNullOrWhiteSpace(query.SourceType);

        // 3. Bucket them per row of the grouping; receipts on account only in customer or party-group rows.
        var rows = new Dictionary<Guid, AgeingRow>();
        if (mode is "customer" or "partygroup")
        {
            var groupNames = mode == "partygroup" ? await partyGroups.NamesAsync(ct) : [];
            (Guid Key, string? Label) Bucket(Guid party, string? name)
            {
                if (mode == "customer") return (party, name);
                var g = groupOf!.GetValueOrDefault(party);
                return g is { } id ? (id, groupNames.GetValueOrDefault(id, "Unknown group")) : (Guid.Empty, "No group");
            }
            foreach (var o in open)
            {
                var (key, label) = Bucket(o.PartyId, o.PartyName);
                rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), query.AsOf, o.DueDate, ReceivablesAsOf.Signed(o));
            }
            if (!filtered)
                foreach (var (party, name, amount) in await receivables.UnallocatedAsync(query.AsOf, query.LegalEntityId, null, ct))
                {
                    if (groupParties is not null && !groupParties.Contains(party)) continue;
                    var (key, label) = Bucket(party, name);
                    rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), query.AsOf, query.AsOf, -amount);
                }
        }
        else
        {
            var unitNames = mode == "businessunit" ? await businessUnits.NamesAsync(ct) : [];
            var labels = new Dictionary<Guid, string>();
            foreach (var o in open)
            {
                var (key, label) = mode == "businessunit"
                    ? (o.BusinessUnitId ?? Guid.Empty, o.BusinessUnitId is { } b ? unitNames.GetValueOrDefault(b, "Unknown unit") : "No business unit")
                    : (StableKey(o.SourceType), string.IsNullOrWhiteSpace(o.SourceType) ? "Direct invoices" : o.SourceType!);
                labels[key] = label;
                rows[key] = Ageing.Add(rows.GetValueOrDefault(key) ?? new AgeingRow(key, label, 0, 0, 0, 0, 0, 0), query.AsOf, o.DueDate, ReceivablesAsOf.Signed(o));
            }
        }

        // 4. Non-zero rows by name, and the totals.
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(query.AsOf, list, totals);
    }

    private static Guid StableKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Guid.Empty;
        return new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant())));
    }
}
