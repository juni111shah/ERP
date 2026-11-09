using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListIndustriesHandler(IOnboardingService onboarding) : IQueryHandler<ListIndustriesQuery, IReadOnlyList<IndustryView>>
{
    public Task<IReadOnlyList<IndustryView>> Handle(ListIndustriesQuery query, CancellationToken ct) => Task.FromResult(onboarding.IndustryViews());
}
