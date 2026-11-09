using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListEmployeesHandler(IEmployeeRepository employees, ILeaveRequestRepository leave, ICurrentUser user, IClock clock) : IQueryHandler<ListEmployeesQuery, PagedResult<EmployeeSummaryDto>>
{
    public async Task<PagedResult<EmployeeSummaryDto>> Handle(ListEmployeesQuery query, CancellationToken ct)
    {
        // 1. The page, with or without the pay columns.
        var page = await employees.PageAsync(query.Paging, user.HasPermission(P.SalaryRead) ? EmployeeSummaryDto.WithPay : EmployeeSummaryDto.WithoutPay, ct: ct);
        var ids = page.Items.Select(x => x.Id).ToList();
        if (ids.Count == 0) return page;

        // 2. Flag who is on approved leave today.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var away = await leave.ListEmployeesOnLeaveAsync(today, ids, ct);
        return page.Map(x => x with { OnLeaveToday = away.Contains(x.Id) });
    }
}
