using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetBillingHandler(BillingOverviews overviews, ICurrentUser user) : IQueryHandler<GetBillingQuery, BillingOverview>
{
    public Task<BillingOverview> Handle(GetBillingQuery query, CancellationToken ct) => overviews.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);
}
