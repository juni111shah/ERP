using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListRatesHandler(IProjectRateRepository rates, ProjectPeople people) : IQueryHandler<ListRatesQuery, IReadOnlyList<RateDto>>
{
    public async Task<IReadOnlyList<RateDto>> Handle(ListRatesQuery query, CancellationToken ct)
    {
        var rows = await rates.ListApplicableAsync(query.ProjectId, ct);
        var names = await people.NamesAsync(rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value), ct);
        return rows.OrderBy(r => r.ProjectId == null ? 1 : 0).ThenBy(r => r.Role).Select(r => RateDto.From(r, r.UserId is { } u ? names.GetValueOrDefault(u) : null)).ToList();
    }
}
