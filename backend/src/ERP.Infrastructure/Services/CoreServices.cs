using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Services;

public sealed class PermissionCatalog : IPermissionCatalog
{
    public PermissionCatalog(ModuleRegistry modules)
    {
        All = CorePermissions.Definitions.Concat(modules.Modules.SelectMany(m => m.Permissions)).DistinctBy(p => p.Key).ToList();
        _keys = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    }

    private readonly HashSet<string> _keys;
    public IReadOnlyList<PermissionDefinition> All { get; }

    public IReadOnlySet<string> Expand(IEnumerable<string> granted)
    {
        var list = granted.ToList();
        if (list.Contains(CorePermissions.All)) return _keys.ToHashSet(StringComparer.Ordinal);
        return list.Where(_keys.Contains).ToHashSet(StringComparer.Ordinal);
    }
}

public sealed class CustomizableEntityRegistry : ICustomizableEntityRegistry
{
    public static IReadOnlyList<CustomizableEntityDefinition> Core { get; } =
    [
        new("party", "Party (customer, vendor)", typeof(Party), "core", CorePermissions.PartiesRead),
        new("item", "Item (product, service)", typeof(Item), "core", CorePermissions.ItemsRead),
        new("legalentity", "Legal entity", typeof(LegalEntity), "core", CorePermissions.EntitiesRead),
        new("businessunit", "Business unit", typeof(BusinessUnit), "core", CorePermissions.BusinessUnitsRead),
        new("account", "Account", typeof(ERP.Domain.Finance.Account), "core", CorePermissions.AccountsRead),
    ];

    private readonly Dictionary<string, CustomizableEntityDefinition> _all;

    public CustomizableEntityRegistry(ModuleRegistry modules)
    {
        _all = new(StringComparer.Ordinal);
        foreach (var d in Core.Concat(modules.Modules.SelectMany(m => m.CustomizableEntities)))
        {
            if (!typeof(IHasCustomFields).IsAssignableFrom(d.ClrType) || !typeof(Entity).IsAssignableFrom(d.ClrType))
                throw new InvalidOperationException($"{d.ClrType.Name} must derive from Entity and implement IHasCustomFields.");
            _all.Add(d.Key, d);
        }
    }

    public IReadOnlyCollection<CustomizableEntityDefinition> All => _all.Values;
    public CustomizableEntityDefinition? Find(string key) => _all.GetValueOrDefault(key);
}

public sealed class ModuleAccess(IAppDbContext db, IOrgContext org, ModuleRegistry registry, IMemoryCache cache) : IModuleAccess
{
    public static string CacheKey(Guid orgId) => $"org:{orgId}:modules";

    public async Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default)
    {
        var m = registry.Find(moduleKey);
        if (m is null) return false;
        if (m.Manifest.IsCore) return true;
        return (await EnabledModulesAsync(ct)).Contains(moduleKey);
    }

    public async Task<IReadOnlySet<string>> EnabledModulesAsync(CancellationToken ct = default)
    {
        if (org.OrgId is not { } orgId) return new HashSet<string>();
        return await cache.GetOrCreateAsync(CacheKey(orgId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var keys = await db.OrganizationModules.AsNoTracking().Where(x => x.IsEnabled).Select(x => x.ModuleKey).ToListAsync(ct);
            return (IReadOnlySet<string>)keys.Where(registry.IsInstalled).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }) ?? new HashSet<string>();
    }
}

public sealed class NumberingService(IAppDbContext db, IClock clock) : INumberingService
{
    public async Task<string> NextAsync(string documentType, Guid? legalEntityId = null, DateOnly? date = null, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(clock.UtcNow);
        var template = await db.NumberSequences.AsNoTracking()
                           .Where(s => s.DocumentType == documentType && (s.LegalEntityId == legalEntityId || s.LegalEntityId == null))
                           .OrderByDescending(s => s.LegalEntityId != null).ThenByDescending(s => s.Year).FirstOrDefaultAsync(ct)
                       ?? throw new ConflictException($"No number sequence is configured for '{documentType}'.");
        var year = template.ResetYearly ? d.Year : 0;
        var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.DocumentType == documentType && s.LegalEntityId == template.LegalEntityId && s.Year == year, ct);
        if (seq is null)
        {
            seq = new NumberSequence { DocumentType = documentType, LegalEntityId = template.LegalEntityId, Prefix = template.Prefix, Pattern = template.Pattern, ResetYearly = template.ResetYearly, Year = year, NextValue = 1 };
            db.NumberSequences.Add(seq);
        }
        var value = seq.NextValue++;
        // Saved with the caller's SaveChanges (same transaction); rowversion makes concurrent allocation fail instead of duplicating.
        return NumberFormatter.Format(seq.Pattern, seq.Prefix, d, value);
    }
}

public sealed class ApprovalService(IAppDbContext db, ICurrentUser user, IClock clock, INotifier notifier) : IApprovalService
{
    public async Task<bool> RequiresApprovalAsync(string documentType, decimal? amount, CancellationToken ct = default)
        => await db.ApprovalPolicies.AnyAsync(p => p.IsActive && p.DocumentType == documentType && (p.MinAmount == null || (amount != null && amount >= p.MinAmount)), ct);

    public async Task<ApprovalRequest> SubmitAsync(string documentType, Guid? documentId, string subject, decimal? amount, string? justification, Guid? legalEntityId, CancellationToken ct = default)
    {
        var req = new ApprovalRequest
        {
            DocumentType = documentType, DocumentId = documentId, Subject = subject, Amount = amount, Justification = justification,
            LegalEntityId = legalEntityId, RequestedBy = user.UserId ?? throw new ForbiddenException(), RequestedAt = clock.UtcNow,
        };
        db.ApprovalRequests.Add(req);
        await db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> DecideAsync(Guid requestId, bool approve, string? comment, CancellationToken ct = default)
    {
        var req = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct) ?? throw new NotFoundException();
        if (!user.HasPermission(CorePermissions.ApprovalsDecide)) throw new ForbiddenException();
        req.Decide(user.UserId!.Value, approve, comment, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(req.RequestedBy, $"Request {(approve ? "approved" : "rejected")}", req.Subject, "/approvals", approve ? "success" : "warning", ct);
        return req;
    }

    public async Task<ApprovalRequest> WithdrawAsync(Guid requestId, CancellationToken ct = default)
    {
        var req = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct) ?? throw new NotFoundException();
        req.Withdraw(user.UserId ?? Guid.Empty);
        await db.SaveChangesAsync(ct);
        return req;
    }
}

/// <summary>Pushes real-time events (implemented with SignalR in the API host).</summary>
public interface INotificationPusher { Task PushAsync(Guid userId, object payload, CancellationToken ct = default); }

public sealed class NullNotificationPusher : INotificationPusher { public Task PushAsync(Guid userId, object payload, CancellationToken ct = default) => Task.CompletedTask; }

public sealed class Notifier(IAppDbContext db, INotificationPusher pusher) : INotifier
{
    public async Task NotifyUserAsync(Guid userId, string title, string? body = null, string? link = null, string severity = "info", CancellationToken ct = default)
    {
        var n = new Notification { UserId = userId, Title = title, Body = body, Link = link, Severity = severity };
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
        await pusher.PushAsync(userId, new { n.Id, n.Title, n.Body, n.Link, n.Severity }, ct);
    }
}

public sealed class LocalFileStore(IConfiguration config) : IFileStore
{
    private string Root => Path.GetFullPath(config["Storage:Root"] ?? Path.Combine(AppContext.BaseDirectory, "storage"));

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default) => Task.FromResult<Stream>(File.OpenRead(PathFor(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var p = PathFor(storageKey);
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(Root, key));
        if (!full.StartsWith(Root, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}

public sealed record SettingView(string Key, string Label, string Module, string Type, JsonElement Value, string? Description);

public sealed class SettingsService(IAppDbContext db, ModuleRegistry modules) : ISettingsService
{
    public static IReadOnlyList<SettingDefinition> CoreSettings { get; } =
    [
        new("core.dateFormat", "Date format", "\"dd MMM yyyy\"", "text"),
        new("core.fiscalYearStartMonth", "Fiscal year start month", "1"),
        new("core.maxAttachmentMb", "Maximum attachment size (MB)", "10"),
        new("core.invoiceTermsDays", "Default payment terms (days)", "15"),
        new("core.journalRequiresApproval", "Manual journals need approval", "false", "bool"),
    ];

    public IEnumerable<(SettingDefinition Def, string Module)> Definitions()
        => CoreSettings.Select(s => (s, "core")).Concat(modules.Modules.SelectMany(m => m.Settings.Select(s => (s, m.Manifest.Key))));

    public async Task<string?> GetRawAsync(string key, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        var rows = await db.Settings.AsNoTracking().Where(s => s.Key == key && (s.LegalEntityId == null || s.LegalEntityId == legalEntityId)).ToListAsync(ct);
        var row = rows.FirstOrDefault(r => r.LegalEntityId == legalEntityId && legalEntityId != null) ?? rows.FirstOrDefault(r => r.LegalEntityId == null);
        return row?.Value ?? Definitions().FirstOrDefault(d => d.Def.Key == key).Def?.DefaultJson;
    }

    public async Task<T?> GetAsync<T>(string key, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, legalEntityId, ct);
        return raw is null ? default : JsonSerializer.Deserialize<T>(raw);
    }

    public async Task SetRawAsync(string key, string jsonValue, Guid? legalEntityId = null, CancellationToken ct = default)
    {
        if (Definitions().All(d => d.Def.Key != key)) throw new FieldValidationException(key, "Unknown setting.");
        using (JsonDocument.Parse(jsonValue)) { }
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key && s.LegalEntityId == legalEntityId, ct);
        if (row is null) db.Settings.Add(new Setting { Key = key, LegalEntityId = legalEntityId, Value = jsonValue });
        else row.Value = jsonValue;
    }
}
