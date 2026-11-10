using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Parties;
using ERP.Domain.Platform;

namespace ERP.Application.Audit;

/// <summary>Reads the before/after JSON of an audit event: which fields moved, what the record is called, and what must stay masked.</summary>
internal static class AuditSnapshot
{
    public const string Deleted = "Deleted";
    private const string Updated = "Updated";

    /// <summary>Fields shown masked (last characters only), consistent with how parties are displayed.</summary>
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase) { "Iban", "IdNumber", "TaxNumber", "AccountNumber", "LabourCardNumber" };
    /// <summary>Bookkeeping columns that add noise to a history.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase) { "Id", "UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "RowVersion", "OrgId", "IsDeleted", "DeletedAt", "DeletedBy" };
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
