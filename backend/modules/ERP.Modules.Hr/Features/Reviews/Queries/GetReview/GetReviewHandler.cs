using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetReviewHandler(IPerformanceReviewRepository reviews, ReviewRules rules) : IQueryHandler<GetReviewQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewQuery query, CancellationToken ct) => await rules.ToDtoAsync(await reviews.GetWithCompetenciesAsync(query.Id, ct), ct);
}
