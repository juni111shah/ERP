using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ListSignupsHandler(ISignupRequestRepository signups, ISubscriptionPlanRepository plans) : IQueryHandler<ListSignupsQuery, PagedResult<SignupRow>>
{
    public async Task<PagedResult<SignupRow>> Handle(ListSignupsQuery query, CancellationToken ct)
    {
        SignupStatus? status = query.Status is { Length: > 0 } text && Enum.TryParse<SignupStatus>(text, true, out var parsed) ? parsed : null;
        var (rows, total) = await signups.ListPageAsync(status, query.Search, query.PageNumber, query.Size, ct);
        var names = await plans.NamesByIdAsync(ct);
        return new(rows.Select(r => SignupRow.From(r, names.GetValueOrDefault(r.PlanId, "Plan"))).ToList(), total, query.PageNumber, query.Size);
    }
}
