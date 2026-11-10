using ERP.Application.Common;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class GetPriceListByIdHandler(IRetailPriceListRepository priceLists) : IQueryHandler<GetPriceListByIdQuery, RetailPriceListDto>
{
    public async Task<RetailPriceListDto> Handle(GetPriceListByIdQuery request, CancellationToken cancellationToken)
    {
        var priceList = await priceLists.GetWithItemsAsync(request.Id, cancellationToken);

        return RetailPriceListDto.From(priceList);
    }
}
