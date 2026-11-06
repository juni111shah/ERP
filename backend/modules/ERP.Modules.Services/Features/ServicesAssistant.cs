using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

internal sealed class EngagementsAssistantReader(IReadRepository<ServiceEngagement> engagements, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "service_engagements";
    public string Description => "Service engagements, newest first: code, title, client, type, status, contract value and dates.";
    public string? ModuleKey => ServicesModule.Key;
    public string Permission => Permissions.EngagementsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = engagements.Query();
        if (search is not null) query = query.Where(x => x.EngagementCode.Contains(search) || x.Title.Contains(search) || x.ClientName.Contains(search));
        return await query.OrderByDescending(x => x.StartDate).Take(take).Select(x => new { Code = x.EngagementCode, x.Title, Client = x.ClientName, Type = x.Type.ToString(), Status = x.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == x.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), x.ContractValue, x.StartDate, x.TargetEndDate, Link = "/services/engagements" }).ToListAsync(ct);
    }
}

internal sealed class RetainersAssistantReader(IReadRepository<RetainerContract> retainers, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "service_retainers";
    public string Description => "Retainer contracts, newest first: number, title, client, billing cycle, status, recurring fee and dates.";
    public string? ModuleKey => ServicesModule.Key;
    public string Permission => Permissions.RetainersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = retainers.Query();
        if (search is not null) query = query.Where(r => r.RetainerNumber.Contains(search) || r.Title.Contains(search) || r.ClientName.Contains(search));
        return await query.OrderByDescending(r => r.StartDate).Take(take).Select(r => new { Number = r.RetainerNumber, r.Title, Client = r.ClientName, Cycle = r.Cycle.ToString(), Status = r.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == r.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), r.RecurringFee, r.StartDate, r.EndDate, Link = "/services/retainers" }).ToListAsync(ct);
    }
}

internal static class ServicesAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Start a service engagement", "Go to Services > Engagements and press New Engagement. Choose the legal entity and client, fill in the contract value and press Save. Change status on its row moves it along.", "/services/engagements", Permissions.EngagementsRead),
        new("Set up a retainer", "Go to Services > Retainers and press New Retainer. Choose the engagement, enter the fee and press Save. Its row offers Pause, Resume and Terminate.", "/services/retainers", Permissions.RetainersRead),
        new("Bill a milestone", "Go to Services > Milestone billing and press Add Milestone, choose the engagement and press Save. When the work is done press Ready to invoice on its row, then Create invoice.", "/services/milestones", Permissions.MilestonesRead),
    ];
}
