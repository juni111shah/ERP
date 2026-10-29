using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Crm.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

public sealed class CrmSearch(
    IReadRepository<CrmLead> leads,
    IReadRepository<CrmOpportunity> opportunities,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => CrmModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.LeadsRead))
        {
            var lList = await leads.Query(new Specification<CrmLead>(l =>
                l.FullName.Contains(term) || l.LeadNumber.Contains(term) || (l.CompanyName != null && l.CompanyName.Contains(term))))
                .Take(limit)
                .Select(l => new { l.Id, l.LeadNumber, l.FullName, l.CompanyName, l.Status })
                .ToListAsync(ct);

            hits.AddRange(lList.Select(l => new SearchHit("Leads", $"{l.LeadNumber} · {l.FullName}", l.CompanyName ?? l.Status.ToString(), "/crm/leads")));
        }

        if (user.HasPermission(Permissions.OpportunitiesRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var oList = await opportunities.Query(new Specification<CrmOpportunity>(o =>
                o.Title.Contains(term) || o.OpportunityNumber.Contains(term) || o.CustomerName.Contains(term)))
                .Take(remaining)
                .Select(o => new { o.Id, o.OpportunityNumber, o.Title, o.CustomerName, o.Stage })
                .ToListAsync(ct);

            hits.AddRange(oList.Select(o => new SearchHit("Opportunities", $"{o.OpportunityNumber} · {o.Title}", $"{o.CustomerName} ({o.Stage})", "/crm/opportunities")));
        }

        return hits;
    }
}
