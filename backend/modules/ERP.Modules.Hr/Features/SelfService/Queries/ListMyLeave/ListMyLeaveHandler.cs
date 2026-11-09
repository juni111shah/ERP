using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListMyLeaveHandler(MyEmployee me, ILeaveRequestRepository requests) : IQueryHandler<ListMyLeaveQuery, PagedResult<LeaveRequestRow>>
{
    public async Task<PagedResult<LeaveRequestRow>> Handle(ListMyLeaveQuery query, CancellationToken ct)
    {
        var mine = (await me.GetAsync(ct)).Id;
        return await requests.PageOfEmployeeAsync(query.Paging, mine, ct);
    }
}
