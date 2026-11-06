using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Audit;

/// <summary>One row of the audit log. It carries no field values, so listing never exposes more than the record's name and which fields moved.</summary>
public sealed record AuditRow(Guid Id, DateTime OccurredAt, Guid? UserId, string? UserName, string Action, string RecordType, string RecordId, string? RecordLabel, string? RecordKind, IReadOnlyList<string> ChangedFields, string? IpAddress);

/// <summary>A field of the record at the time of the event. <c>Changed</c> is false for values the event left alone.</summary>
public sealed record AuditFieldDiff(string Field, string? Old, string? New, bool Changed);

public sealed record AuditEventDetail(AuditRow Event, IReadOnlyList<AuditFieldDiff> Fields, string? Before, string? After, IReadOnlyList<AuditRow> SameSave, int RecordEventCount);

public sealed record AuditUserOption(Guid Id, string Name);

public sealed record AuditFacets(IReadOnlyList<string> RecordTypes, IReadOnlyList<string> Actions, IReadOnlyList<AuditUserOption> Users, int Today, int Last7Days, int DeletionsLast7Days, int ActiveUsersLast7Days);

/// <summary>The audit log, newest first by default. Search, filter and sort come from the [Searchable]/[Filterable]/[Sortable] fields of <see cref="AuditEvent"/>.</summary>
public sealed record ListAuditEvents : PagedQuery, IQuery<PagedResult<AuditRow>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}

/// <summary>One event: every field before and after with sensitive values masked, and what else the same save wrote.</summary>
public sealed record GetAuditEvent(Guid Id) : IQuery<AuditEventDetail>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}

/// <summary>What the log can be filtered by in this workspace, with a short summary of recent activity.</summary>
public sealed record GetAuditFacets : IQuery<AuditFacets>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}

internal sealed class AuditLogHandler(IReadRepository<AuditEvent> audits, IUserDirectory directory, IClock clock) :
    IRequestHandler<ListAuditEvents, PagedResult<AuditRow>>,
    IRequestHandler<GetAuditEvent, AuditEventDetail>,
    IRequestHandler<GetAuditFacets, AuditFacets>
{
    private const int SameSaveLimit = 25;

    public async Task<PagedResult<AuditRow>> Handle(ListAuditEvents q, CancellationToken ct)
    {
        var page = await audits.PageAsync(q, ct: ct);
        var names = await UserNames(page.Items, ct);
        return page.Map(a => ToRow(a, names));
    }

    public async Task<AuditEventDetail> Handle(GetAuditEvent q, CancellationToken ct)
    {
        var a = await audits.GetAsync(q.Id, ct);
        var sameSave = await audits.Query(new Specification<AuditEvent>(x => x.OccurredAt == a.OccurredAt && x.UserId == a.UserId && x.Id != a.Id))
            .OrderBy(x => x.RecordType).Take(SameSaveLimit).ToListAsync(ct);
        var names = await UserNames([a, .. sameSave], ct);
        var count = await audits.CountAsync(x => x.RecordType == a.RecordType && x.RecordId == a.RecordId, ct);
        return new AuditEventDetail(ToRow(a, names), AuditSnapshot.Fields(a), AuditSnapshot.Masked(a.Before), AuditSnapshot.Masked(a.After), sameSave.Select(x => ToRow(x, names)).ToList(), count);
    }

    public async Task<AuditFacets> Handle(GetAuditFacets q, CancellationToken ct)
    {
        var all = audits.Query();
        var recordTypes = await all.Select(a => a.RecordType).Distinct().OrderBy(t => t).ToListAsync(ct);
        var actions = await all.Select(a => a.Action).Distinct().OrderBy(t => t).ToListAsync(ct);
        var userIds = await all.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToListAsync(ct);
        var users = (await directory.ListMembersAsync(userIds, ct)).Select(u => new AuditUserOption(u.Id, u.FullName)).OrderBy(u => u.Name).ToList();

        var today = clock.UtcNow.Date;
        var week = today.AddDays(-6);
        return new AuditFacets(recordTypes, actions, users,
            await audits.CountAsync(a => a.OccurredAt >= today, ct),
            await audits.CountAsync(a => a.OccurredAt >= week, ct),
            await audits.CountAsync(a => a.OccurredAt >= week && a.Action == AuditSnapshot.Deleted, ct),
            await all.Where(a => a.OccurredAt >= week && a.UserId != null).Select(a => a.UserId).Distinct().CountAsync(ct));
    }

    private async Task<Dictionary<Guid, string>> UserNames(IEnumerable<AuditEvent> events, CancellationToken ct)
    {
        var ids = events.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        return ids.Count == 0 ? [] : (await directory.ListMembersAsync(ids, ct)).ToDictionary(u => u.Id, u => u.FullName);
    }

    private static AuditRow ToRow(AuditEvent a, Dictionary<Guid, string> names)
    {
        var snapshot = AuditSnapshot.Latest(a);
        return new AuditRow(a.Id, a.OccurredAt, a.UserId, a.UserId is { } u ? names.GetValueOrDefault(u) : null, a.Action, a.RecordType, a.RecordId,
            AuditSnapshot.Label(snapshot), snapshot.GetValueOrDefault("Direction"), AuditSnapshot.ChangedFields(a), a.IpAddress);
    }
}

/// <summary>Reads the before/after JSON of an audit event: which fields moved, what the record is called, and what must stay masked.</summary>
internal static class AuditSnapshot
{
    public const string Deleted = "Deleted";
    private const string Updated = "Updated";

    /// <summary>Fields shown masked (last characters only), consistent with how parties are displayed.</summary>
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase) { "Iban", "IdNumber", "TaxNumber", "AccountNumber", "LabourCardNumber" };
    /// <summary>Bookkeeping columns that add noise to a history.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase) { "UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion", "OrgId", "IsDeleted", "DeletedAt", "DeletedBy" };
    /// <summary>The fields that name a record, best first.</summary>
    private static readonly string[] LabelFields = ["Number", "Name", "FullName", "Title", "Subject", "Code", "Email", "Reference"];

    /// <summary>The fields an event changed, without the bookkeeping columns. A soft delete writes only those, so it lists none.</summary>
    public static IReadOnlyList<string> ChangedFields(AuditEvent a) =>
        string.IsNullOrEmpty(a.ChangedFields) ? [] : a.ChangedFields.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(f => !Noise.Contains(f)).Order().ToList();

    /// <summary>Fields whose value differs between before and after, oldest value first.</summary>
    public static List<FieldChange> Changes(AuditEvent a)
    {
        var before = Parse(a.Before);
        var after = Parse(a.After);
        IEnumerable<string> fields = a.Action == Updated && !string.IsNullOrEmpty(a.ChangedFields) ? a.ChangedFields.Split(',', StringSplitOptions.RemoveEmptyEntries) : after.Keys.Union(before.Keys);
        return fields.Where(f => !Noise.Contains(f)).Select(f => new FieldChange(f, Show(f, before.GetValueOrDefault(f)), Show(f, after.GetValueOrDefault(f))))
            .Where(c => c.Old != c.New).OrderBy(c => c.Field).ToList();
    }

    /// <summary>Every field of the record at the event, the changed ones flagged. Changed fields first, then the rest by name.</summary>
    public static List<AuditFieldDiff> Fields(AuditEvent a)
    {
        var before = Parse(a.Before);
        var after = Parse(a.After);
        return after.Keys.Union(before.Keys).Where(f => !Noise.Contains(f))
            .Select(f =>
            {
                var (rawOld, rawNew) = (before.GetValueOrDefault(f), after.GetValueOrDefault(f));
                return new AuditFieldDiff(f, Show(f, rawOld), Show(f, rawNew), rawOld != rawNew);
            })
            .OrderByDescending(d => d.Changed).ThenBy(d => d.Field, StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, string?> Latest(AuditEvent a)
    {
        var after = Parse(a.After);
        return after.Count > 0 ? after : Parse(a.Before);
    }

    public static string? Label(Dictionary<string, string?> snapshot)
    {
        foreach (var field in LabelFields)
            if (snapshot.TryGetValue(field, out var v) && !string.IsNullOrWhiteSpace(v)) return Show(field, v);
        return null;
    }

    /// <summary>The stored JSON with sensitive values masked, or null when the event has none.</summary>
    public static string? Masked(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root) return null;
            foreach (var name in root.Select(p => p.Key).Where(Sensitive.Contains).ToList())
                root[name] = Masking.Mask(root[name]?.ToString());
            return root.ToJsonString();
        }
        catch (JsonException) { return null; }
    }

    private static string? Show(string field, string? value) => Sensitive.Contains(field) ? Masking.Mask(value) : value;

    private static Dictionary<string, string?> Parse(string? json)
    {
        var result = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
                result[p.Name] = p.Value.ValueKind switch { JsonValueKind.Null => null, JsonValueKind.String => p.Value.GetString(), _ => p.Value.ToString() };
        }
        catch (JsonException) { /* an unreadable snapshot just shows no field changes */ }
        return result;
    }
}
