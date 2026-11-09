using System.Text.Json;
using ERP.Application.Assistant;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Dashboard;
using ERP.Application.Finance;
using ERP.Application.Search;
using ERP.Application.Workflow;
using ERP.Modularity.Features;
using ERP.Modules.Assistant.Domain;
using MediatR;

namespace ERP.Modules.Assistant.Features;

/// <summary>Keeps a tool's answer small, and treats "not allowed" as an answer rather than a failure.</summary>
internal static class ToolAnswers
{
    private const int MaxLength = 6000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize(object value)
    {
        var text = JsonSerializer.Serialize(value, Json);
        return text.Length <= MaxLength ? text : text[..MaxLength] + " ...(cut short: ask the person to narrow it down, or point them to the screen)";
    }

    public static string? ReadText(string argumentsJson, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            return doc.RootElement.Clone().Text(name);
        }
        catch (JsonException) { return null; }
    }

    public const string NotAllowed = "This person does not have permission to see that. Tell them so plainly, without guessing at the answer.";
}

internal sealed class SearchRecordsTool(ISender sender) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("search_records", "Finds records by name, code or number across the whole workspace. Records are usually stored in English.",
        """{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""");
    public string StatusText => "Searching your records";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var query = ToolAnswers.ReadText(argumentsJson, "query");
        if (query is null) return "Give some words to search for.";
        var result = await sender.Send(new GlobalSearch(query), ct);
        return result.Hits.Count == 0 ? "Nothing matches." : ToolAnswers.Serialize(result.Hits.Take(20));
    }
}

internal sealed class ListRecordsTool(AssistantCatalog catalog) : IAssistantTool
{
    private const int PageSize = 20;
    public ChatToolDefinition Definition { get; } = new("list_records", "Lists records of one kind, with an optional search. The kinds this person can use are listed in the context.",
        """{"type":"object","properties":{"kind":{"type":"string"},"search":{"type":"string"}},"required":["kind"]}""");
    public string StatusText => "Looking through your records";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var reader = ToolAnswers.ReadText(argumentsJson, "kind") is { } kind ? await catalog.FindReaderAsync(kind, ct) : null;
        if (reader is null) return ToolAnswers.NotAllowed;
        return ToolAnswers.Serialize(await reader.ListAsync(ToolAnswers.ReadText(argumentsJson, "search"), PageSize, ct));
    }
}

internal sealed class RunReportTool(ISender sender, ICurrentUser user, IClock clock) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("run_report",
        "Runs a finance report for the legal entity selected on the page. Reports: receivables_ageing (who owes money), payables_ageing (who is owed), income_statement, balance_sheet, trial_balance, cash_movements, tax_summary. Dates are yyyy-MM-dd; the period defaults to this year.",
        """{"type":"object","properties":{"report":{"type":"string","enum":["receivables_ageing","payables_ageing","income_statement","balance_sheet","trial_balance","cash_movements","tax_summary"]},"from":{"type":"string"},"to":{"type":"string"}},"required":["report"]}""");
    public string StatusText => "Running the report";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var to = DateOnly.TryParse(ToolAnswers.ReadText(argumentsJson, "to"), out var t) ? t : today;
        var from = DateOnly.TryParse(ToolAnswers.ReadText(argumentsJson, "from"), out var f) ? f : new DateOnly(to.Year, 1, 1);
        var entity = user.LegalEntityId;
        try
        {
            // Each of these requests carries its own permission, which the mediator pipeline enforces.
            object? result = ToolAnswers.ReadText(argumentsJson, "report") switch
            {
                "receivables_ageing" => await sender.Send(new GetArAgeing(to, entity), ct),
                "payables_ageing" => await sender.Send(new GetApAgeing(to, entity), ct),
                "income_statement" => await sender.Send(new GetIncomeStatementQuery(from, to, entity), ct),
                "balance_sheet" => await sender.Send(new GetBalanceSheetQuery(to, entity), ct),
                "trial_balance" => await sender.Send(new GetTrialBalanceQuery(from, to, entity), ct),
                "cash_movements" => await sender.Send(new GetCashMovementsQuery(from, to, entity), ct),
                "tax_summary" => await sender.Send(new GetTaxSummaryQuery(from, to, entity), ct),
                _ => null,
            };
            return result is null ? "There is no report with that name." : ToolAnswers.Serialize(result);
        }
        catch (ForbiddenException) { return ToolAnswers.NotAllowed; }
        catch (FieldValidationException ex) { return string.Join(" ", ex.Errors.SelectMany(e => e.Value)); } // for example a scope that mixes base currencies
    }
}

internal sealed class GetOverviewTool(ISender sender, ICurrentUser user) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("get_overview", "The main figures and what needs attention today, as on the dashboard this person sees.", """{"type":"object","properties":{}}""");
    public string StatusText => "Checking your overview";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var d = await sender.Send(new GetDashboard(user.LegalEntityId), ct);
        return ToolAnswers.Serialize(new { d.AsOf, Figures = d.Kpis.Select(k => new { k.Label, k.Value, k.Caption, k.Link }), NeedsAttention = d.Alerts.Select(a => new { a.Category, a.Urgency, a.Message, a.Link }) });
    }
}

internal sealed class ListApprovalsTool(ISender sender) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("list_approvals", "Approvals waiting for this person's decision, or their own requests if they cannot decide any.", """{"type":"object","properties":{}}""");
    public string StatusText => "Checking approvals";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var list = await sender.Send(new ListApprovals(null), ct);
        return list.Count == 0 ? "There is nothing waiting." : ToolAnswers.Serialize(new { Link = "/approvals", Items = list.Take(20).Select(a => new { a.Subject, a.Amount, Status = a.Status.ToString(), a.RequestedAt }) });
    }
}

internal sealed class FindScreenTool(ISender sender) : IAssistantTool
{
    public ChatToolDefinition Definition { get; } = new("find_screen", "Finds where something is in this person's menu, with a link to it. Use it for 'where do I...' and 'how do I...' questions.",
        """{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""");
    public string StatusText => "Looking through the menu";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        var menu = await sender.Send(new GetNavigationMenu(), ct);
        var words = (ToolAnswers.ReadText(argumentsJson, "query") ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = menu.SelectMany(g => g.Items.Select(i => new { Group = g.Label, i.Label, Link = i.Route }))
            .Where(i => words.Any(w => i.Label.Contains(w, StringComparison.OrdinalIgnoreCase) || (i.Group?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false))).Take(10).ToList();
        return hits.Count > 0 ? ToolAnswers.Serialize(hits) : "No screen matches. This person's whole menu: " + ToolAnswers.Serialize(menu.SelectMany(g => g.Items.Select(i => new { i.Label, Link = i.Route })));
    }
}

/// <summary>
/// The only way the model can touch data, and it changes nothing: it records a suggestion for the person to confirm with a button.
/// The model never sees the suggestion's id, so nothing it writes can confirm one, even if a record's text tells it to.
/// </summary>
internal sealed class ProposeActionTool(AssistantCatalog catalog, IRepository<AssistantPendingAction> pending, ICurrentUser user, IClock clock, Guid conversationId) : IAssistantTool
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public List<AssistantPendingAction> Proposed { get; } = [];

    public ChatToolDefinition Definition { get; } = new("propose_action",
        "Suggests a change for the person to confirm. Nothing happens until they press Confirm on the card they are shown. The actions this person can use, and their arguments, are listed in the context.",
        """{"type":"object","properties":{"action":{"type":"string"},"arguments":{"type":"object"}},"required":["action","arguments"]}""");
    public string StatusText => "Preparing the change";

    public async Task<string> RunAsync(string argumentsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        var key = doc.RootElement.Text("action");
        var action = key is null ? null : await catalog.FindActionAsync(key, ct);
        if (action is null) return "That change is not available to this person. Tell them so.";
        var arguments = doc.RootElement.TryGetProperty("arguments", out var a) ? a.Clone() : default;
        AssistantActionPreview preview;
        try { preview = await action.PreviewAsync(arguments, ct); }
        catch (Exception e) when (e is ConflictException or NotFoundException or ForbiddenException) { return "This change cannot go ahead: " + e.Message; }

        var now = clock.UtcNow;
        var row = new AssistantPendingAction
        {
            ConversationId = conversationId, UserId = user.UserId!.Value, ActionKey = action.Key, ArgumentsJson = arguments.GetRawText(), Title = preview.Title,
            LinesJson = JsonSerializer.Serialize(preview.Lines, Json), Status = AssistantPendingAction.Pending, CreatedAt = now, ExpiresAt = now + Lifetime,
        };
        pending.Add(row);
        await pending.SaveChangesAsync(ct);
        Proposed.Add(row);
        return "A card with this change is now on screen. It has NOT been done. Ask the person to check it and press Confirm. Do not say it is done.";
    }
}
