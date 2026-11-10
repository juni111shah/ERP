using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetAcquisitionOverviewHandler(IAcquisitionRepository acquisitions, IFundingSourceRepository sources, IFundingInstalmentRepository instalments)
    : IQueryHandler<GetAcquisitionOverviewQuery, AcquisitionOverviewDto>
{
    public async Task<AcquisitionOverviewDto> Handle(GetAcquisitionOverviewQuery q, CancellationToken ct)
    {
        // 1. The live acquisitions, their funding and the principal repaid on it.
        var done = await acquisitions.ListLiveAmountsAsync(ct);
        var ids = done.Select(a => a.Id).ToList();
        var funding = await sources.ListPrincipalsOfAcquisitionsAsync(ids, ct);
        var fundingIds = funding.Select(f => f.Id).ToList();
        var repaid = await instalments.PaidPrincipalOfSourcesAsync(fundingIds, ct);

        // 2. The totals.
        var financing = funding.Sum(f => f.Principal);
        return new AcquisitionOverviewDto(done.Sum(a => a.PurchasePrice), done.Count, done.Sum(a => a.DownPayment), financing, financing - repaid, done.Sum(a => a.PurchasePrice - a.DownPayment) - financing);
    }
}
