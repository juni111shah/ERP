using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Modules.Crm.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

/// <summary>Never returns or searches a lead's email or phone.</summary>
internal sealed class LeadsAssistantReader(IReadRepository<CrmLead> leads, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "crm_leads";
    public string Description => "CRM leads, newest first: number, name, company, source, status, estimated value and owner.";
    public string? ModuleKey => CrmModule.Key;
    public string Permission => Permissions.LeadsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = leads.Query();
        if (search is not null) query = query.Where(l => l.LeadNumber.Contains(search) || l.FullName.Contains(search) || (l.CompanyName != null && l.CompanyName.Contains(search)));
        return await query.OrderByDescending(l => l.CreatedAt).Take(take).Select(l => new { Number = l.LeadNumber, Name = l.FullName, Company = l.CompanyName, Source = l.Source.ToString(), Status = l.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == l.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), l.EstimatedValue, Owner = l.AssignedUserName, Link = "/crm/leads" }).ToListAsync(ct);
    }
}

internal sealed class OpportunitiesAssistantReader(IReadRepository<CrmOpportunity> opportunities, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "crm_opportunities";
    public string Description => "Sales pipeline deals, newest first: number, title, customer, stage, probability, expected value, target close date and owner.";
    public string? ModuleKey => CrmModule.Key;
    public string Permission => Permissions.OpportunitiesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = opportunities.Query();
        if (search is not null) query = query.Where(o => o.OpportunityNumber.Contains(search) || o.Title.Contains(search) || o.CustomerName.Contains(search));
        return await query.OrderByDescending(o => o.CreatedAt).Take(take).Select(o => new { Number = o.OpportunityNumber, o.Title, Customer = o.CustomerName, Stage = o.Stage.ToString(), o.ProbabilityPercent, Currency = entities.Query(null).Where(e => e.Id == o.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), o.ExpectedRevenue, o.ExpectedCloseDate, Owner = o.AssignedUserName, Link = "/crm/opportunities" }).ToListAsync(ct);
    }
}

internal static class CrmAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Add a lead and convert it", "Go to CRM > Leads & contacts, press Add New Lead and press Save Lead. Press Convert on its row to turn the lead into a deal in the pipeline.", "/crm/leads", Permissions.LeadsRead),
        new("Work the sales pipeline", "Go to CRM > Sales pipeline and press New Opportunity, then Create Deal. Move to shifts a deal between stages, and Close Deal records whether it was won or lost.", "/crm/opportunities", Permissions.OpportunitiesRead),
        new("Log a call, meeting or note", "Go to CRM > Activity logs, press Log Activity, fill it in and press Log Activity again to save. Mark Done closes a follow-up.", "/crm/interactions", Permissions.InteractionsRead),
    ];
}

/// <summary>Adds a note to a customer's activity log, with the same command as the Activity logs screen.</summary>
internal sealed class LogNoteAssistantAction(ISender sender, IValidator<LogInteractionCommand> validator, AssistantScope scope, IReadRepository<Party> parties, ICurrentUser user) : IAssistantAction
{
    public string Key => "log_customer_note";
    public string Description => "Add a note to a customer's activity log. Find the customer's id with list_records first.";
    public string ParametersJsonSchema => """{"type":"object","properties":{"customer_id":{"type":"string"},"subject":{"type":"string"},"details":{"type":"string"}},"required":["customer_id","subject"]}""";
    public string? ModuleKey => CrmModule.Key;
    public string Permission => Permissions.InteractionsManage;

    private async Task<(LogInteractionCommand Command, string CustomerName, string EntityName)> BuildAsync(JsonElement a, AssistantActionContext context, CancellationToken ct)
    {
        var party = a.Id("customer_id") is { } id ? await parties.FindAsync(id, ct) : null;
        if (party is null) throw new ConflictException(context.T("That customer could not be found.", "تعذر العثور على هذا العميل."));
        var entity = await scope.LegalEntityAsync(context, ct);
        return (new LogInteractionCommand(entity.Id, "Customer", party.Id, InteractionType.Note, a.Text("subject") ?? "", a.Text("details"), null, user.UserId, null, null, true, null), party.Name, entity.Name);
    }

    public async Task<AssistantActionPreview> PreviewAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var (command, customer, entityName) = await BuildAsync(arguments, context, ct);
        await validator.EnsureValidAsync(command, ct);
        var lines = new List<AssistantActionLine> { new(context.LegalEntityLabel, entityName), new(context.T("Customer", "العميل"), customer), new(context.T("Subject", "الموضوع"), command.Subject) };
        if (command.Details is { } details) lines.Add(new(context.T("Details", "التفاصيل"), details));
        return new(context.T("Add a note to the activity log", "إضافة ملاحظة إلى سجل الأنشطة"), lines, command.LegalEntityId);
    }

    public async Task<AssistantActionResult> ExecuteAsync(JsonElement arguments, AssistantActionContext context, CancellationToken ct)
    {
        var (command, customer, _) = await BuildAsync(arguments, context, ct);
        await sender.Send(command, ct);
        return new(context.T($"The note was added for {customer}.", $"تمت إضافة الملاحظة إلى {customer}."), "/crm/interactions");
    }
}
