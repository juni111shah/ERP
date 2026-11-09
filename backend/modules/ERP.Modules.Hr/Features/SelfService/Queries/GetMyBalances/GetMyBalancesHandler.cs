using ERP.Application.Abstractions;
using ERP.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Hr.Features;

internal sealed class GetMyBalancesHandler(MyEmployee me, IServiceProvider services, IClock clock) : IQueryHandler<GetMyBalancesQuery, IReadOnlyList<LeaveBalanceRow>>
{
    public async Task<IReadOnlyList<LeaveBalanceRow>> Handle(GetMyBalancesQuery query, CancellationToken ct)
    {
        // 1. The employee comes from the link.
        var mine = await me.GetAsync(ct);

        // 2. The balances handler runs directly (not through the pipeline): the link is the authorisation.
        return await ActivatorUtilities.CreateInstance<GetLeaveBalancesHandler>(services).Handle(new GetLeaveBalancesQuery(query.Year ?? clock.UtcNow.Year, mine.Id, null), ct);
    }
}
