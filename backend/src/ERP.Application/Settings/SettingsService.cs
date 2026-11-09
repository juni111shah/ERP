using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Settings;

/// <summary>Settings store (JSON values per workspace, optionally per legal entity). Changes are saved by the calling command's unit of work.</summary>
public sealed class SettingsService(IRepository<Setting> settings, ISettingsCatalog catalog) : ISettingsService
{
    public static IReadOnlyList<SettingDefinition> CoreSettings { get; } =
    [
        new("core.dateFormat", "Date format", "\"dd MMM yyyy\"", "text"),
        new("core.fiscalYearStartMonth", "Fiscal year start month", "1"),
        new("core.maxAttachmentMb", "Maximum attachment size (MB)", "10"),
        new("core.invoiceTermsDays", "Default payment terms (days)", "15"),
        new("finance.chequeDueAlertDays", "Cheque due alert (days)", "7"),
        new("core.journalRequiresApproval", "Manual journals need approval", "false", "bool"),
        new("core.creditLimitWarnOnly", "Warn instead of refusing when a customer goes over their credit limit", "false", "bool"),
        // Edited on the chart of accounts screen (mask editor), not as a plain setting; stored in the same key/value table.
        new("finance.coa.numbering", "Chart of accounts numbering", "{\"mode\":\"free\",\"segments\":[4,2,2],\"separator\":\"-\"}", "json"),
        // Edited on the business setup screen.
        new("core.uoms", "Measures for items", "[]", "json"),
        new("core.itemCategories", "Item categories", "[]", "json"),
        new("core.disabledCapabilities", "Core areas switched off", "[]", "json"),
        new("core.dashboardFocus", "Owner dashboard order", "[]", "json"),
        // Which steps of the demo data seeder have finished, so a re-run skips them. Empty outside demo workspaces.
        new("core.demoSeedCheckpoints", "Demo data progress", "[]", "json"),
    ];

    public async Task<string?> GetRawAsync(string key, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        var rows = await settings.Query(new Specification<Setting>(s => s.Key == key && (s.LegalEntityId == null || s.LegalEntityId == legalEntityId))).ToListAsync(ct);
        var row = rows.FirstOrDefault(r => r.LegalEntityId == legalEntityId && legalEntityId != null) ?? rows.FirstOrDefault(r => r.LegalEntityId == null);
        return row?.Value ?? catalog.Definitions().FirstOrDefault(d => d.Def.Key == key).Def?.DefaultJson;
    }

    public async Task<T?> GetAsync<T>(string key, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, legalEntityId, ct);
        return raw is null ? default : JsonSerializer.Deserialize<T>(raw);
    }

    public async Task SetRawAsync(string key, string jsonValue, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        if (catalog.Definitions().All(d => d.Def.Key != key)) throw new FieldValidationException(key, "Unknown setting.");
        using (JsonDocument.Parse(jsonValue)) { } // must be valid JSON
        var row = await settings.Tracked(new Specification<Setting>(s => s.Key == key && s.LegalEntityId == legalEntityId)).FirstOrDefaultAsync(ct);
        if (row is null) settings.Add(new Setting { Key = key, LegalEntityId = legalEntityId, Value = jsonValue });
        else row.Value = jsonValue;
    }
}
