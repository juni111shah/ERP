using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using ERP.Application.Search;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Leads whose follow-up date has arrived are reminded to the brokerage team once per date.</summary>
internal sealed class CrmReminders(IReadRepository<Lead> leads) : IReminderSource
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var due = await leads.Query(new Specification<Lead>(l => l.FollowUpOn != null && l.FollowUpOn <= today && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)).ToListAsync(ct);
        return due.Select(l => new ReminderCandidate($"lead-followup:{l.Id}:{l.FollowUpOn:yyyyMMdd}", $"Follow up with lead {l.Name}", l.Interest ?? l.Phone, "/real-estate/leads", P.CrmManage, Severity: "info")).ToList();
    }
}

/// <summary>Leads in the global search.</summary>
internal sealed class CrmSearch(IReadRepository<Lead> leads, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        if (!user.HasPermission(P.CrmRead)) return [];
        return (await leads.Query(new Specification<Lead>(l => l.Name.Contains(term) || (l.Phone != null && l.Phone.Contains(term)) || (l.Email != null && l.Email.Contains(term)))).Take(limit).Select(l => new { l.Name, l.Stage, l.Phone }).ToListAsync(ct))
            .Select(l => new SearchHit("Leads", l.Name, $"{l.Stage} · {l.Phone}", $"/real-estate/leads?search={Uri.EscapeDataString(l.Name)}")).ToList();
    }
}
