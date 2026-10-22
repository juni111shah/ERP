using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

/// <summary>An entity type that accepts custom fields (core or module-provided).</summary>
public sealed record CustomizableEntityDefinition(string Key, string Label, Type ClrType, string ModuleKey = "core", string? Permission = null);

/// <summary>Registry of customizable entity types: "party" → Party, "core.party" → ...</summary>
public interface ICustomizableEntityRegistry
{
    IReadOnlyCollection<CustomizableEntityDefinition> All { get; }
    CustomizableEntityDefinition? Find(string key);
}

public sealed class CustomFieldChoice
{
    public string Value { get; set; } = default!;
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Typed view over CustomFieldDefinition.Options JSON.</summary>
public sealed class CustomFieldOptions
{
    public List<CustomFieldChoice>? Choices { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public int? Scale { get; set; }
    public int? MaxSelections { get; set; }
    public string? Target { get; set; }
    public JsonNode? Default { get; set; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public static CustomFieldOptions Parse(string? json)
        => string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<CustomFieldOptions>(json, Json) ?? new();

    public string Serialize() => JsonSerializer.Serialize(this, new JsonSerializerOptions(Json) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
}

/// <summary>
/// Validates custom field values against the workspace's definitions and produces the JSON to store.
/// Rules: keys must exist and be active; types are strict (no "12" for a number); unknown keys are rejected;
/// on update, values for keys not sent (including inactive fields) are kept (merge semantics).
/// </summary>
public static class CustomFieldValidator
{
    public const int MaxFieldsPerEntityType = 50;
    public const int MaxPayloadBytes = 16 * 1024;
    public const int MaxTextLength = 4000;
    public const int MaxChoices = 200;
    public static readonly Regex KeyFormat = new("^cf_[a-z0-9_]{1,60}$", RegexOptions.Compiled);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public sealed record Result(bool IsValid, string? Json, IReadOnlyDictionary<string, string[]> Errors);

    /// <summary>Validates a definition itself (key format, options consistency, safe regex).</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateDefinition(CustomFieldDefinition def)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string f, string m) => errors[f] = errors.TryGetValue(f, out var e) ? [.. e, m] : [m];

        if (string.IsNullOrWhiteSpace(def.Key) || !KeyFormat.IsMatch(def.Key))
            Add("key", "Key must be lower snake case starting with 'cf_' (max 63 characters), e.g. cf_vip_tier.");
        if (string.IsNullOrWhiteSpace(def.Label) || def.Label.Length > 100) Add("label", "Label is required (max 100 characters).");
        if (string.IsNullOrWhiteSpace(def.EntityType)) Add("entityType", "Entity type is required.");

        CustomFieldOptions opts;
        try { opts = CustomFieldOptions.Parse(def.Options); }
        catch (JsonException) { Add("options", "Options must be valid JSON."); return errors; }

        if (def.DataType is CustomFieldDataType.Select or CustomFieldDataType.MultiSelect)
        {
            if (opts.Choices is not { Count: > 0 }) Add("options.choices", "Select fields need at least one choice.");
            else
            {
                if (opts.Choices.Count > MaxChoices) Add("options.choices", $"At most {MaxChoices} choices.");
                if (opts.Choices.Any(c => string.IsNullOrWhiteSpace(c.Value))) Add("options.choices", "Every choice needs a value.");
                if (opts.Choices.GroupBy(c => c.Value).Any(g => g.Count() > 1)) Add("options.choices", "Choice values must be unique.");
            }
        }
        if (def.DataType == CustomFieldDataType.Lookup && string.IsNullOrWhiteSpace(opts.Target))
            Add("options.target", "Lookup fields need a target entity type.");
        if (opts.Min is { } min && opts.Max is { } max && min > max) Add("options", "Min cannot be greater than max.");
        if (opts.MaxLength is > MaxTextLength) Add("options.maxLength", $"Max length cannot exceed {MaxTextLength}.");
        if (opts.Pattern is { } pattern)
        {
            if (pattern.Length > 200) Add("options.pattern", "Pattern is too long.");
            else
            {
                try
                {
                    var rx = new Regex(pattern, RegexOptions.None, RegexTimeout);
                    // Reject catastrophic patterns: probe with a worst-case style input.
                    rx.IsMatch(new string('a', 30) + "!");
                }
                catch (ArgumentException) { Add("options.pattern", "Pattern is not a valid regular expression."); }
                catch (RegexMatchTimeoutException) { Add("options.pattern", "Pattern is too expensive to evaluate."); }
            }
        }
        if (!string.IsNullOrWhiteSpace(def.DefaultValue))
        {
            try
            {
                if (ValidateValue(def, JsonNode.Parse(def.DefaultValue), out _) is { } err) Add("defaultValue", err);
            }
            catch (JsonException) { Add("defaultValue", "Default value must be JSON (e.g. \"gold\", 10, true)."); }
        }
        return errors;
    }

    /// <summary>
    /// Validates <paramref name="incoming"/> (a JSON object of key → value) against <paramref name="definitions"/> and merges it
    /// over <paramref name="existingJson"/>. <paramref name="lookupExists"/> checks lookup targets inside the caller's workspace.
    /// </summary>
    public static Result Validate(
        IReadOnlyCollection<CustomFieldDefinition> definitions,
        JsonObject? incoming,
        string? existingJson,
        bool isCreate,
        Func<string, Guid, bool>? lookupExists = null)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string msg) => errors[$"customFields.{key}"] = errors.TryGetValue($"customFields.{key}", out var e) ? [.. e, msg] : [msg];

        var merged = ParseObject(existingJson) ?? new JsonObject();
        incoming ??= new JsonObject();

        if (incoming.ToJsonString().Length > MaxPayloadBytes)
            return new(false, null, new Dictionary<string, string[]> { ["customFields"] = [$"Custom fields payload exceeds {MaxPayloadBytes / 1024} KB."] });

        var byKey = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);

        foreach (var (key, node) in incoming)
        {
            if (!byKey.TryGetValue(key, out var def)) { Add(key, "Unknown field."); continue; }
            if (!def.IsActive) { Add(key, "This field is inactive and cannot be changed."); continue; }

            var error = ValidateValue(def, node, out var normalized, lookupExists);
            if (error is not null) { Add(key, error); continue; }

            if (normalized is null) merged.Remove(key);
            else merged[key] = normalized;
        }

        foreach (var def in definitions.Where(d => d.IsActive && d.Required))
        {
            var sent = incoming.ContainsKey(def.Key);
            if (!isCreate && !sent) continue; // merge semantics: an untouched field keeps its value
            if (!merged.TryGetPropertyValue(def.Key, out var v) || v is null)
            {
                if (isCreate && !sent && (ParseNode(def.DefaultValue) ?? CustomFieldOptions.Parse(def.Options).Default) is { } dflt)
                {
                    merged[def.Key] = dflt.DeepClone();
                    continue;
                }
                if (!errors.ContainsKey($"customFields.{def.Key}")) Add(def.Key, $"{def.Label} is required.");
            }
        }

        if (errors.Count > 0) return new(false, null, errors);
        return new(true, merged.Count == 0 ? null : merged.ToJsonString(), errors);
    }

    /// <summary>Returns an error message, or null when valid. <paramref name="normalized"/> is null for "clear the value".</summary>
    public static string? ValidateValue(CustomFieldDefinition def, JsonNode? node, out JsonNode? normalized, Func<string, Guid, bool>? lookupExists = null)
    {
        normalized = null;
        if (node is null) return null;
        var opts = CustomFieldOptions.Parse(def.Options);

        switch (def.DataType)
        {
            case CustomFieldDataType.Text:
            {
                if (!TryString(node, out var s)) return "Must be text.";
                s = s.Trim();
                if (s.Length == 0) return null;
                var max = Math.Min(opts.MaxLength ?? MaxTextLength, MaxTextLength);
                if (s.Length > max) return $"Must be at most {max} characters.";
                if (opts.MinLength is { } minLen && s.Length < minLen) return $"Must be at least {minLen} characters.";
                if (opts.Pattern is { } p)
                {
                    try { if (!Regex.IsMatch(s, $"^(?:{p})$", RegexOptions.None, RegexTimeout)) return "Has an invalid format."; }
                    catch (RegexMatchTimeoutException) { return "Has an invalid format."; }
                }
                normalized = JsonValue.Create(s);
                return null;
            }
            case CustomFieldDataType.Number:
            case CustomFieldDataType.Decimal:
            {
                if (node is not JsonValue v || v.GetValueKind() != JsonValueKind.Number) return "Must be a number.";
                if (!decimal.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return "Number is out of range.";
                if (def.DataType == CustomFieldDataType.Number && d != decimal.Truncate(d)) return "Must be a whole number.";
                if (def.DataType == CustomFieldDataType.Decimal)
                {
                    var scale = opts.Scale ?? 2;
                    if (decimal.Round(d, scale) != d) return $"Must have at most {scale} decimal places.";
                    if (Math.Abs(d) >= 1e16m) return "Number is out of range.";
                }
                else if (d is > long.MaxValue or < long.MinValue) return "Number is out of range.";
                if (opts.Min is { } min && d < min) return $"Must be at least {min.ToString(CultureInfo.InvariantCulture)}.";
                if (opts.Max is { } max2 && d > max2) return $"Must be at most {max2.ToString(CultureInfo.InvariantCulture)}.";
                normalized = def.DataType == CustomFieldDataType.Number ? JsonValue.Create((long)d) : JsonValue.Create(d);
                return null;
            }
            case CustomFieldDataType.Date:
            {
                if (!TryString(node, out var s)) return "Must be a date (yyyy-MM-dd).";
                if (!DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return "Must be a date (yyyy-MM-dd).";
                normalized = JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return null;
            }
            case CustomFieldDataType.DateTime:
            {
                if (!TryString(node, out var s) || !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto) || !s.Contains('T'))
                    return "Must be an ISO 8601 date-time with offset (e.g. 2026-10-22T10:00:00+04:00).";
                normalized = JsonValue.Create(dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                return null;
            }
            case CustomFieldDataType.Bool:
            {
                if (node is not JsonValue v || v.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False)) return "Must be true or false.";
                normalized = JsonValue.Create(v.GetValue<bool>());
                return null;
            }
            case CustomFieldDataType.Select:
            {
                if (!TryString(node, out var s)) return "Must be one of the allowed choices.";
                var choice = opts.Choices?.FirstOrDefault(c => c.Value == s);
                if (choice is null || !choice.IsActive) return "Must be one of the allowed choices.";
                normalized = JsonValue.Create(s);
                return null;
            }
            case CustomFieldDataType.MultiSelect:
            {
                if (node is not JsonArray arr) return "Must be a list of choices.";
                var values = new List<string>();
                foreach (var item in arr)
                {
                    if (item is null || !TryString(item, out var s)) return "Must be a list of choices.";
                    var choice = opts.Choices?.FirstOrDefault(c => c.Value == s);
                    if (choice is null || !choice.IsActive) return $"'{s}' is not an allowed choice.";
                    if (values.Contains(s)) return "Choices cannot repeat.";
                    values.Add(s);
                }
                if (opts.MaxSelections is { } maxSel && values.Count > maxSel) return $"Choose at most {maxSel}.";
                if (values.Count == 0) return null;
                normalized = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
                return null;
            }
            case CustomFieldDataType.Lookup:
            {
                if (!TryString(node, out var s) || !Guid.TryParse(s, out var id)) return "Must be a record id.";
                if (lookupExists is not null && !lookupExists(opts.Target ?? "", id)) return "The referenced record was not found.";
                normalized = JsonValue.Create(id.ToString());
                return null;
            }
            default:
                return "Unsupported field type.";
        }
    }

    private static bool TryString(JsonNode node, out string value)
    {
        value = "";
        if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String) { value = v.GetValue<string>(); return true; }
        return false;
    }

    private static JsonNode? ParseNode(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    /// <summary>Definitions that apply to a record in <paramref name="legalEntityId"/> (entity-restricted fields elsewhere are ignored).</summary>
    public static IReadOnlyCollection<CustomFieldDefinition> ForLegalEntity(IEnumerable<CustomFieldDefinition> defs, Guid? legalEntityId)
        => defs.Where(d => d.LegalEntityId is null || d.LegalEntityId == legalEntityId).ToList();

    public static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 8 }) as JsonObject;
    }

    /// <summary>Values to show the caller: only active definitions (inactive values stay stored but hidden).</summary>
    public static JsonObject Project(string? json, IEnumerable<CustomFieldDefinition> definitions, bool includeInactive = false)
    {
        var src = ParseObject(json) ?? new JsonObject();
        var result = new JsonObject();
        foreach (var def in definitions.Where(d => includeInactive || d.IsActive))
            if (src.TryGetPropertyValue(def.Key, out var v) && v is not null) result[def.Key] = v.DeepClone();
        return result;
    }
}
