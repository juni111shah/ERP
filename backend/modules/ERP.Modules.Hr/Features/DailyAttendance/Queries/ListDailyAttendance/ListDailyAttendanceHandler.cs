using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListDailyAttendanceHandler(IAttendanceLogRepository logs) : IQueryHandler<ListDailyAttendanceQuery, IReadOnlyList<DailyAttendanceDto>>
{
    public async Task<IReadOnlyList<DailyAttendanceDto>> Handle(ListDailyAttendanceQuery query, CancellationToken ct)
    {
        var start = new DateOnly(query.Year, query.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var rows = await logs.ListBetweenAsync(start, end, query.EmployeeId, query.LegalEntityId, ct);
        return rows.OrderBy(l => l.Date).ThenBy(l => l.EmployeeId).Select(DailyAttendanceDto.From).ToList();
    }
}
