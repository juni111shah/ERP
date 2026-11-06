using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Parties;
using ERP.Modules.Sales.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

internal sealed class QuotesAssistantReader(IReadRepository<SalesQuote> quotes) : IAssistantReader
{
    public string Key => "sales_quotes";
    public string Description => "Sales quotes, newest first: number, customer, date, status and total.";
    public string? ModuleKey => SalesModule.Key;
    public string Permission => P.QuotesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = quotes.Query();
        if (search is not null) query = query.Where(q => q.Number.Contains(search) || q.CustomerName.Contains(search));
        return await query.OrderByDescending(q => q.QuoteDate).Take(take).Select(q => new { q.Number, Customer = q.CustomerName, q.QuoteDate, Status = q.Status.ToString(), q.Currency, q.TotalAmount, Link = "/sales/quotes/" + q.Id }).ToListAsync(ct);
    }
}

internal sealed class OrdersAssistantReader(IReadRepository<SalesOrder> orders) : IAssistantReader
{
    public string Key => "sales_orders";
    public string Description => "Sales orders, newest first: number, customer, date, status and total.";
    public string? ModuleKey => SalesModule.Key;
    public string Permission => P.OrdersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = orders.Query();
        if (search is not null) query = query.Where(o => o.Number.Contains(search) || o.CustomerName.Contains(search));
        return await query.OrderByDescending(o => o.OrderDate).Take(take).Select(o => new { o.Number, Customer = o.CustomerName, o.OrderDate, Status = o.Status.ToString(), o.Currency, o.TotalAmount, Link = "/sales/orders/" + o.Id }).ToListAsync(ct);
    }
}

/// <summary>Drafts a quote. It stays a draft, sends the same command as the Quotes screen, and nothing goes to the customer.</summary>
internal sealed class CreateQuoteAssistantAction(ISender sender, IValidator<QuoteInput> validator, AssistantScope scope, IReadRepository<Party> parties, IClock clock) : IAssistantAction
{
    public string Key => "create_draft_quote";
    public string Description => "Draft a sales quote for a customer. Find the customer's id with list_records first.";
    public string ParametersJsonSchema => """{"type":"object","properties":{"customer_id":{"type":"string"},"notes":{"type":"string"},"lines":{"type":"array","items":{"type":"object","properties":{"description":{"type":"string"},"quantity":{"type":"number"},"unit_price":{"type":"number"}},"required":["description","quantity","unit_price"]}}},"required":["customer_id","lines"]}""";
    public string? ModuleKey => SalesModule.Key;
    public string Permission => P.QuotesManage;

    private async Task<(QuoteInput Input, string EntityName)> BuildAsync(JsonElement a, AssistantActionContext context, CancellationToken ct)
    {
        var party = a.Id("customer_id") is { } id ? await parties.FindAsync(id, ct) : null;
        if (party is null) throw new ConflictException(context.T("That customer could not be found.", "تعذر العثور على هذا العميل."));
        var entity = await scope.LegalEntityAsync(context, ct);
        var lines = new List<QuoteLineInput>();
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("lines", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var l in list.EnumerateArray())
                lines.Add(new QuoteLineInput(null, "", l.Text("description") ?? "", l.Number("quantity") ?? 0, "ea", l.Number("unit_price") ?? -1, 0, 0));
        return (new QuoteInput(entity.Id, null, party.Id, party.Name, null, null, DateOnly.FromDateTime(clock.UtcNow), null, entity.BaseCurrency, a.Text("notes"), null, lines), entity.Name);
    }

    public async Task<AssistantActionPreview> PreviewAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var (input, entityName) = await BuildAsync(arguments, context, ct);
        await validator.EnsureValidAsync(input, ct);
        var lines = new List<AssistantActionLine> { new(context.LegalEntityLabel, entityName), new(context.T("Customer", "العميل"), input.CustomerName) };
        lines.AddRange(input.Lines.Select(l => new AssistantActionLine(l.Description, $"{l.Quantity:0.##} x {input.Currency} {l.UnitPrice:0.00}")));
        lines.Add(new(context.T("Total before tax", "الإجمالي قبل الضريبة"), $"{input.Currency} {input.Lines.Sum(l => l.Quantity * l.UnitPrice):0.00}"));
        return new(context.T("Create a draft quote", "إنشاء مسودة عرض سعر"), lines, input.LegalEntityId);
    }

    public async Task<AssistantActionResult> ExecuteAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var quote = await sender.Send(new CreateQuote((await BuildAsync(arguments, context, ct)).Input), ct);
        return new(context.T($"Draft quote {quote.Number} was created. Nothing has been sent to the customer.", $"تم إنشاء مسودة عرض السعر {quote.Number}. لم يُرسل شيء إلى العميل."), $"/sales/quotes/{quote.Id}");
    }
}
