using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetLeaveRequestHandler(ILeaveRequestRepository requests, LeaveRequestWorkflow leave) : IQueryHandler<GetLeaveRequestQuery, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(GetLeaveRequestQuery query, CancellationToken ct) => await leave.ToDtoAsync(await requests.GetAsync(query.Id, ct), ct);
}
