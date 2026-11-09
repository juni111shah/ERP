using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class GetWholesalePriceByIdHandler(IWholesalePriceMatrixRepository matrix) : IQueryHandler<GetWholesalePriceByIdQuery, WholesalePriceMatrixDto?>
{
    public async Task<WholesalePriceMatrixDto?> Handle(GetWholesalePriceByIdQuery request, CancellationToken cancellationToken)
    {
        var price = await matrix.FindAsync(request.Id, cancellationToken);
        return price is null ? null : WholesalePriceMatrixDto.From(price);
    }
}
