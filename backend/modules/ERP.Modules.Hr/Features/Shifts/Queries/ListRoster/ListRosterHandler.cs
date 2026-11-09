using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListRosterHandler(IShiftRosterRepository roster) : IQueryHandler<ListRosterQuery, IReadOnlyList<RosterDto>>
{
    public async Task<IReadOnlyList<RosterDto>> Handle(ListRosterQuery query, CancellationToken ct)
    {
        var start = new DateOnly(query.Year, query.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var rows = await roster.ListOverlappingAsync(start, end, ct);
        return rows.OrderBy(r => r.FromDate).Select(RosterDto.From).ToList();
    }
}
