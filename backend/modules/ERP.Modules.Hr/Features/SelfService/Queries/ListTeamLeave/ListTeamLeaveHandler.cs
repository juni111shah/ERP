using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListTeamLeaveHandler(MyEmployee me, IEmployeeRepository employees, ILeaveRequestRepository requests) : IQueryHandler<ListTeamLeaveQuery, PagedResult<LeaveRequestRow>>
{
    public async Task<PagedResult<LeaveRequestRow>> Handle(ListTeamLeaveQuery query, CancellationToken ct)
    {
        // 1. The people who report to me.
        var mine = (await me.GetAsync(ct)).Id;
        var reports = await employees.ListReportIdsAsync(mine, ct);

        // 2. Their requests, of one status when asked.
        LeaveStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status)) status = Enum.TryParse<LeaveStatus>(query.Status, true, out var parsed) ? parsed : throw new FieldValidationException("status", "Unknown leave status.");
        return await requests.PageOfEmployeesAsync(query.Paging, reports, status, ct);
    }
}
