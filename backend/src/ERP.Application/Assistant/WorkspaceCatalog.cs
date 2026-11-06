using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Assistant;

/// <summary>
/// One kind of record the workspace assistant may look through. Core and each module register their own, so the core never knows an industry.
/// It returns only safe fields: names, numbers and amounts, never contact details, bank details or identity numbers.
/// </summary>
public interface IAssistantReader
{
    string Key { get; }
    string Description { get; }
    /// <summary>Module that must be enabled; null = core.</summary>
    string? ModuleKey { get; }
    string Permission { get; }
    Task<object> ListAsync(string? search, int take, CancellationToken ct);
}

/// <summary>A short how-to, in plain words. <paramref name="Permission"/> hides it from people who could not use that screen anyway.</summary>
public sealed record AssistantHelpArticle(string Title, string Body, string? Link = null, string? Permission = null);

/// <summary>How-to articles a module owns, so the core never holds an industry's words. Register one from the module's ConfigureServices.</summary>
public interface IAssistantHelp
{
    /// <summary>Module that must be enabled; null = core.</summary>
    string? ModuleKey { get; }
    IReadOnlyList<AssistantHelpArticle> Articles { get; }
}

public sealed record AssistantHelpPack(string? ModuleKey, IReadOnlyList<AssistantHelpArticle> Articles) : IAssistantHelp;

public sealed record AssistantActionLine(string Label, string Value);
/// <summary><c>LegalEntityId</c> is stored with the card, so Confirm runs against the entity the person saw. Null for a change that belongs to no legal entity.</summary>
public sealed record AssistantActionPreview(string Title, IReadOnlyList<AssistantActionLine> Lines, Guid? LegalEntityId = null);
public sealed record AssistantActionResult(string Message, string? Link);

/// <summary>Who a card is for: the person's language and, once the card exists, the legal entity stored on it.</summary>
public sealed record AssistantActionContext(string Language, Guid? LegalEntityId = null)
{
    public static string Normalize(string? language) => language?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true ? "ar" : "en";
    public string T(string english, string arabic) => Language == "ar" ? arabic : english;
    public string LegalEntityLabel => T("Legal entity", "الكيان القانوني");
}

/// <summary>
/// A change the assistant may suggest. The model can only ask for a preview: running it takes a person's click on a card built from that preview,
/// and it then sends the same command the screens send, so permissions, validation and approval rules all apply as usual.
/// </summary>
public interface IAssistantAction
{
    string Key { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    string? ModuleKey { get; }
    string Permission { get; }
    /// <summary>Throws <see cref="ConflictException"/> with a message for the person when the request cannot go ahead.</summary>
    Task<AssistantActionPreview> PreviewAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct);
    Task<AssistantActionResult> ExecuteAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct);
}

/// <summary>
/// What this person may use right now. In-process calls skip the web layer's module and permission checks, so every reader and action goes through here.
/// </summary>
public sealed class AssistantCatalog(IEnumerable<IAssistantReader> readers, IEnumerable<IAssistantAction> actions, IEnumerable<IAssistantHelp> help, IModuleAccess modules, ICurrentUser user)
{
    private async Task<bool> AllowedAsync(string? moduleKey, string permission, CancellationToken ct)
        => user.HasPermission(permission) && (moduleKey is null || await modules.IsEnabledAsync(moduleKey, ct));

    public async Task<IReadOnlyList<IAssistantReader>> ReadersAsync(CancellationToken ct)
    {
        var allowed = new List<IAssistantReader>();
        foreach (var r in readers.OrderBy(r => r.Key, StringComparer.Ordinal)) if (await AllowedAsync(r.ModuleKey, r.Permission, ct)) allowed.Add(r);
        return allowed;
    }

    public async Task<IReadOnlyList<IAssistantAction>> ActionsAsync(CancellationToken ct)
    {
        var allowed = new List<IAssistantAction>();
        foreach (var a in actions.OrderBy(a => a.Key, StringComparer.Ordinal)) if (await AllowedAsync(a.ModuleKey, a.Permission, ct)) allowed.Add(a);
        return allowed;
    }

    /// <summary>The articles that share the most words with the question, from modules this workspace uses and screens this person can open.</summary>
    public async Task<IReadOnlyList<AssistantHelpArticle>> FindHelpAsync(string question, int take, CancellationToken ct)
    {
        var words = HelpWords(question);
        if (words.Count == 0) return [];
        var found = new List<(AssistantHelpArticle Article, int Score)>();
        foreach (var pack in help)
        {
            if (pack.ModuleKey is not null && !await modules.IsEnabledAsync(pack.ModuleKey, ct)) continue;
            foreach (var a in pack.Articles)
            {
                if (a.Permission is not null && !user.HasPermission(a.Permission)) continue;
                var score = 3 * words.Intersect(HelpWords(a.Title)).Count() + words.Intersect(HelpWords(a.Body)).Count();
                if (score > 0) found.Add((a, score));
            }
        }
        return found.OrderByDescending(x => x.Score).Take(take).Select(x => x.Article).ToList();
    }

    private static readonly HashSet<string> CommonHelpWords = new(StringComparer.OrdinalIgnoreCase)
        { "the", "and", "for", "how", "what", "can", "does", "you", "your", "are", "with", "that", "this", "have", "will", "there", "from", "about", "when", "any", "where", "who", "want", "need", "not", "use" };

    private static HashSet<string> HelpWords(string text)
        => System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value)
            .Where(w => w.Length >= 3 && !CommonHelpWords.Contains(w)).Select(w => w.Length > 4 && w.EndsWith('s') && !w.EndsWith("ss", StringComparison.Ordinal) ? w[..^1] : w).ToHashSet();

    public async Task<IAssistantReader?> FindReaderAsync(string key, CancellationToken ct)
        => (await ReadersAsync(ct)).FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));

    public async Task<IAssistantAction?> FindActionAsync(string key, CancellationToken ct)
        => (await ActionsAsync(ct)).FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
}

public static class AssistantArguments
{
    public static string? Text(this JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

    public static decimal? Number(this JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static Guid? Id(this JsonElement args, string name) => Guid.TryParse(args.Text(name), out var id) ? id : null;

    /// <summary>Runs the command's own validator during the preview, so a bad request is refused before anyone is asked to confirm it.</summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T command, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(command, ct);
        if (!result.IsValid) throw new ConflictException(result.Errors[0].ErrorMessage);
    }
}

/// <summary>The legal entity a new document belongs to: the one stored on the card, else the one selected at the top of the page, or the only one this person has.</summary>
public sealed class AssistantScope(ICurrentUser user, IReadRepository<LegalEntity> entities)
{
    public async Task<LegalEntity> LegalEntityAsync(AssistantActionContext context, CancellationToken ct)
    {
        if (context.LegalEntityId is { } stored) return await entities.FindAsync(stored, ct) ?? throw new ConflictException(context.T("That legal entity no longer exists.", "لم يعد هذا الكيان القانوني موجوداً."));
        if (user.LegalEntityId is { } selected) return await entities.GetAsync(selected, ct);
        var allowed = await entities.Query().Where(e => user.AllowedLegalEntityIds == null || user.AllowedLegalEntityIds.Contains(e.Id)).Take(2).ToListAsync(ct);
        return allowed.Count == 1 ? allowed[0] : throw new ConflictException(context.T("Choose a legal entity at the top of the page first.", "اختر كياناً قانونياً من أعلى الصفحة أولاً."));
    }
}

internal sealed class PartiesAssistantReader(IReadRepository<Party> parties) : IAssistantReader
{
    public string Key => "parties";
    public string Description => "Customers and suppliers: id, code, name and role. Use the id when suggesting a change for one of them.";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.PartiesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = parties.Query();
        if (search is not null) query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search));
        return await query.OrderBy(p => p.Name).Take(take).Select(p => new { p.Id, p.Code, p.Name, Kind = p.Kind.ToString(), Roles = p.Roles.ToString(), Link = "/parties/" + p.Id }).ToListAsync(ct);
    }
}

internal sealed class InvoicesAssistantReader(IReadRepository<Invoice> invoices) : IAssistantReader
{
    public string Key => "invoices";
    public string Description => "Sales invoices, newest first: number, customer, dates, total, what is still owed and status.";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.InvoicesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = invoices.Query();
        if (search is not null) query = query.Where(i => i.Number.Contains(search) || (i.PartyName != null && i.PartyName.Contains(search)));
        return await query.OrderByDescending(i => i.InvoiceDate).Take(take)
            .Select(i => new { i.Number, Customer = i.PartyName, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.Total, Owed = i.Total - i.Paid, Status = i.Status.ToString(), Link = "/finance/invoices/" + i.Id }).ToListAsync(ct);
    }
}

internal sealed class BillsAssistantReader(IReadRepository<Bill> bills) : IAssistantReader
{
    public string Key => "bills";
    public string Description => "Supplier bills, newest first: number, supplier, due date, total, what is still to pay and status.";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.BillsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = bills.Query();
        if (search is not null) query = query.Where(b => b.Number.Contains(search) || (b.PartyName != null && b.PartyName.Contains(search)));
        return await query.OrderByDescending(b => b.BillDate).Take(take)
            .Select(b => new { b.Number, Supplier = b.PartyName, b.BillDate, b.DueDate, b.CurrencyCode, b.Total, ToPay = b.Total - b.Paid, Status = b.Status.ToString(), Link = "/finance/bills/" + b.Id }).ToListAsync(ct);
    }
}

internal sealed class ItemsAssistantReader(IReadRepository<Item> items) : IAssistantReader
{
    public string Key => "items";
    public string Description => "Products and services: SKU, name, unit and sales price.";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.ItemsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = items.Query();
        if (search is not null) query = query.Where(i => i.Sku.Contains(search) || i.Name.Contains(search));
        return await query.OrderBy(i => i.Name).Take(take).Select(i => new { i.Sku, i.Name, i.Uom, i.SalesPrice }).ToListAsync(ct);
    }
}

/// <summary>Adds a customer or supplier. It sends the same command as the Parties screen.</summary>
internal sealed class CreatePartyAssistantAction(ISender sender, IValidator<CreateParty> validator) : IAssistantAction
{
    public string Key => "create_party";
    public string Description => "Add a new customer or supplier.";
    public string ParametersJsonSchema => """{"type":"object","properties":{"name":{"type":"string"},"role":{"type":"string","enum":["customer","supplier"]},"kind":{"type":"string","enum":["company","individual"]}},"required":["name","role"]}""";
    public string? ModuleKey => null;
    public string Permission => CorePermissions.PartiesWrite;

    private static CreateParty Build(JsonElement a)
    {
        var role = a.Text("role")?.ToLowerInvariant() switch { "customer" => PartyRoles.Customer, "supplier" or "vendor" => PartyRoles.Vendor, _ => PartyRoles.None };
        var kind = string.Equals(a.Text("kind"), "individual", StringComparison.OrdinalIgnoreCase) ? PartyKind.Individual : PartyKind.Company;
        return new CreateParty(new PartyInput(a.Text("name") ?? "", kind, role, null, null, null, null, null, null, null, null));
    }

    public async Task<AssistantActionPreview> PreviewAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var command = Build(arguments);
        await validator.EnsureValidAsync(command, ct);
        var i = command.Input;
        var title = i.Roles == PartyRoles.Vendor ? context.T("Add a new supplier", "إضافة مورد جديد") : context.T("Add a new customer", "إضافة عميل جديد");
        return new(title, [new(context.T("Name", "الاسم"), i.Name), new(context.T("Type", "النوع"), i.Kind == PartyKind.Individual ? context.T("Individual", "فرد") : context.T("Company", "شركة"))]);
    }

    public async Task<AssistantActionResult> ExecuteAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var party = await sender.Send(Build(arguments), ct);
        return new(context.T($"{party.Name} was added with code {party.Code}.", $"تمت إضافة {party.Name} بالرمز {party.Code}."), $"/parties/{party.Id}");
    }
}
