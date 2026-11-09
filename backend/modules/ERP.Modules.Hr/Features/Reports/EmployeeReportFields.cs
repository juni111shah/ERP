namespace ERP.Modules.Hr.Features;

/// <summary>
/// Custom-field keys the reports read until employee master data has first-class fields for them: <c>cf_contract_type</c> (text) and <c>cf_date_of_birth</c> (yyyy-MM-dd).
/// </summary>
internal static class EmployeeReportFields
{
    public const string ContractType = "cf_contract_type";
    public const string DateOfBirth = "cf_date_of_birth";

    /// <summary>The real date-of-birth field, or the custom field some workspaces used before it existed.</summary>
    public static DateOnly? BirthDate(DateOnly? real, string? customFields)
        => real ?? (DateOnly.TryParse(Text(customFields, DateOfBirth), out var parsed) ? parsed : null);

    public static string? Text(string? customFields, string key)
    {
        if (customFields is not { Length: > 0 } json) return null;
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)?[key];
            return node is null ? null : (node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : node.ToString())?.Trim() is { Length: > 0 } v ? v : null;
        }
        catch { return null; }
    }
}
