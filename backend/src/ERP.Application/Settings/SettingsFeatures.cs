using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using MediatR;
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

public sealed record SettingView(string Key, string Label, string Module, string Type, string? Description, JsonElement Value);

public sealed record ListSettings : IQuery<IReadOnlyList<SettingView>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.SettingsRead;
}

/// <summary>Sets several settings at once. Core keys need core.settings.write; a module's keys also accept &lt;module&gt;.settings.write.</summary>
public sealed record UpdateSettings(Dictionary<string, JsonElement> Values) : ICommand;

internal sealed class SettingsHandlers(ISettingsService settings, ISettingsCatalog catalog, IModuleAccess modules, ICurrentUser user, IUnitOfWork unitOfWork) :
    IRequestHandler<ListSettings, IReadOnlyList<SettingView>>,
    IRequestHandler<UpdateSettings, Unit>
{
    public async Task<IReadOnlyList<SettingView>> Handle(ListSettings q, CancellationToken ct)
    {
        var enabled = await modules.EnabledModulesAsync(ct);
        var list = new List<SettingView>();
        foreach (var (def, module) in catalog.Definitions().Where(d => d.Module == "core" || enabled.Contains(d.Module)))
        {
            var raw = await settings.GetRawAsync(def.Key, ct: ct) ?? "null";
            list.Add(new(def.Key, def.Label, module, def.Type, def.Description, JsonSerializer.Deserialize<JsonElement>(raw)));
        }
        return list;
    }

    public async Task<Unit> Handle(UpdateSettings cmd, CancellationToken ct)
    {
        foreach (var (key, value) in cmd.Values)
        {
            var module = key.Split('.')[0];
            if (module != "core")
            {
                if (!await modules.IsEnabledAsync(module, ct)) throw new FieldValidationException(key, "That module is not enabled.");
                if (!user.HasPermission(CorePermissions.SettingsWrite) && !user.HasPermission($"{module}.settings.write")) throw new ForbiddenException();
            }
            else if (!user.HasPermission(CorePermissions.SettingsWrite)) throw new ForbiddenException();
            await settings.SetRawAsync(key, value.GetRawText(), ct: ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
