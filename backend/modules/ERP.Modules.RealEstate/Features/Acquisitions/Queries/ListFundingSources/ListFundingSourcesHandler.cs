using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ListFundingSourcesHandler(IFundingSourceRepository sources, AcquisitionDtoFactory factory) : IQueryHandler<ListFundingSourcesQuery, IReadOnlyList<FundingSourceDto>>
{
    public async Task<IReadOnlyList<FundingSourceDto>> Handle(ListFundingSourcesQuery q, CancellationToken ct)
    {
        var list = await sources.ListAllByDueAsync(ct);
        var result = new List<FundingSourceDto>();
        foreach (var s in list) result.Add(await factory.BuildSourceAsync(s, includeInstalments: false, ct));
        return result;
    }
}
