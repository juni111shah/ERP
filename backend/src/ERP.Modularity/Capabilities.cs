using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modularity;

/// <summary>
/// An optional area of the core a business can switch off. Modules are switched on and off per workspace; these are the same idea for what
/// ships in the core. Switching one off hides its menu entries and, where <see cref="CapabilityGateMetadata"/> is set, closes its API.
/// The ledger underneath always keeps working, because other documents post to it.
/// </summary>
public sealed record CoreCapability(string Key, string Label, string Description, IReadOnlyList<string> MenuItems);

public static class CoreCapabilities
{
    public const string SettingKey = "core.disabledCapabilities";

    public static IReadOnlyList<CoreCapability> All { get; } =
    [
        new("receivables", "Customers and receivables", "Customer invoices, receipts and the customer list.", ["sales.customers", "finance.invoices", "finance.receipts"]),
        new("payables", "Vendors and payables", "Vendor bills, payments and the vendor list.", ["purchases.vendors", "finance.bills", "finance.payments"]),
        new("banking", "Cash and bank", "Bank accounts, statements and reconciliation.", ["finance.bank"]),
        new("cheques", "Cheque register", "Post-dated and issued cheques.", ["finance.cheques"]),
        new("budgets", "Budgets", "Annual budgets and budget against actual.", ["finance.budgets"]),
        new("journals", "Manual journals", "Manual journal entries. Documents still post to the ledger.", ["finance.journals"]),
        new("tax", "Tax codes", "Tax codes on documents.", ["finance.taxcodes"]),
        new("catalog", "Products and services", "The items list.", ["core.items"]),
        new("partygroups", "Party groups", "Grouping of customers and vendors.", ["core.partygroups"]),
        new("approvals", "Approval workflows", "The approvals inbox and rules.", ["core.approvals", "core.approvalrules"]),
        new("documents", "Document vault", "All stored documents with expiry.", ["core.documents"]),
        new("import", "Data import", "Bulk import from CSV files.", ["core.import"]),
        new("messages", "Message log", "Reminders and messages sent.", ["core.messages"]),
    ];

    public static CoreCapability? Find(string key) => All.FirstOrDefault(c => c.Key == key);
}

public interface ICapabilityAccess
{
    Task<IReadOnlySet<string>> DisabledAsync(CancellationToken ct = default);
    Task<IReadOnlySet<string>> HiddenMenuItemsAsync(CancellationToken ct = default);
}

public sealed class CapabilityAccess(ISettingsService settings) : ICapabilityAccess
{
    private IReadOnlySet<string>? _disabled;

    public async Task<IReadOnlySet<string>> DisabledAsync(CancellationToken ct = default)
    {
        if (_disabled is not null) return _disabled;
        var raw = await settings.GetRawAsync(CoreCapabilities.SettingKey, ct: ct);
        var keys = string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<string[]>(raw) ?? [];
        return _disabled = keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> HiddenMenuItemsAsync(CancellationToken ct = default)
    {
        var disabled = await DisabledAsync(ct);
        return CoreCapabilities.All.Where(c => disabled.Contains(c.Key)).SelectMany(c => c.MenuItems).ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>Endpoint metadata marking a core endpoint group as belonging to an optional capability.</summary>
public sealed record CapabilityGateMetadata(string Capability);

/// <summary>Same contract as the module gate: 403 when the area is switched off for the workspace, and its data is kept.</summary>
public sealed class CapabilityGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var gate = http.GetEndpoint()?.Metadata.GetMetadata<CapabilityGateMetadata>();
        if (gate is not null && http.User.Identity?.IsAuthenticated == true)
        {
            var access = http.RequestServices.GetRequiredService<ICapabilityAccess>();
            if ((await access.DisabledAsync(http.RequestAborted)).Contains(gate.Capability))
            {
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status403Forbidden, "Not enabled",
                    $"'{CoreCapabilities.Find(gate.Capability)?.Label ?? gate.Capability}' is switched off for this workspace. Its data is kept and returns when it is switched on again.",
                    ModuleProblems.ModuleDisabledType);
                return;
            }
        }
        await next(http);
    }
}

/// <summary>What choosing a business type or enabling a module brings with it.</summary>
public static class ModuleProvisioning
{
    public const string UnitsKey = "core.uoms";
    public const string ItemCategoriesKey = "core.itemCategories";
    public const string DashboardFocusKey = "core.dashboardFocus";

    /// <summary>The installed modules asked for, plus everything they need, dependencies first.</summary>
    public static IReadOnlyList<string> WithRequirements(ModuleRegistry modules, IEnumerable<string> wanted)
    {
        var ordered = new List<string>();
        void Visit(string key, HashSet<string> path)
        {
            if (ordered.Contains(key, StringComparer.OrdinalIgnoreCase) || !path.Add(key)) return;
            if (modules.Find(key) is not { } module) return;
            foreach (var required in module.Manifest.Requires ?? []) Visit(required, path);
            if (!module.Manifest.IsCore) ordered.Add(module.Manifest.Key);
        }
        foreach (var key in wanted) Visit(key, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    /// <summary>The settings a business type sets up: units, categories, areas switched off and dashboard order.</summary>
    public static IEnumerable<(string Key, string Value)> ProfileSettings(IndustryTemplate template)
    {
        if (template.Units is { Count: > 0 } units) yield return (UnitsKey, JsonSerializer.Serialize(units));
        if (template.ItemCategories is { Count: > 0 } categories) yield return (ItemCategoriesKey, JsonSerializer.Serialize(categories));
        if (template.CapabilitiesOff is { Count: > 0 } off) yield return (CoreCapabilities.SettingKey, JsonSerializer.Serialize(off));
        if (template.DashboardFocus is { Count: > 0 } focus) yield return (DashboardFocusKey, JsonSerializer.Serialize(focus));
    }
}
