using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using MediatR;

namespace ERP.Modules.Projects.Features;

/// <summary>Project profitability for the owner, from the same report as the Projects module.</summary>
public sealed class ProjectsOwnerSection(ISender sender, ICurrentUser user) : IOwnerDashboardSection
{
    public string ModuleKey => ProjectsModule.Key;

    public async Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(P.ReportsRead)) return [];
        var rows = await sender.Send(new GetProfitabilityReportQuery(scope.From, scope.To, null), ct);
        var billed = rows.Sum(r => r.Billed);
        var margin = rows.Sum(r => r.Margin);
        var ordered = rows.OrderByDescending(r => r.Billed + r.Cost).Take(8).ToList();
        return
        [
            new("project-profit", "Project profitability",
                [new("project-billed", "Billed on projects", billed, Link: "/projects/reports"), new("project-cost", "Project cost", rows.Sum(r => r.Cost)), new("project-margin", "Project margin", margin), new("project-margin-pct", "Margin", billed == 0 ? 0 : Math.Round(margin / billed * 100, 1), "percent")],
                new(["Project", "Billed", "Cost", "Margin"], ordered.Select(r => (IReadOnlyList<string>)[$"{r.Number} {r.Name}", r.Billed.ToString("0.00"), r.Cost.ToString("0.00"), r.Margin.ToString("0.00")]).ToList(), ["text", "money", "money", "money"])),
        ];
    }
}
