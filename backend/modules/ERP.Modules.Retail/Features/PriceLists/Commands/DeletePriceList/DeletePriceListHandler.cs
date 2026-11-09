using ERP.Application.Common;
using ERP.Modules.Retail.Repositories;
using MediatR;

namespace ERP.Modules.Retail.Features;

internal sealed class DeletePriceListHandler(IRetailPriceListRepository priceLists) : ICommandHandler<DeletePriceListCommand>
{
    public async Task<Unit> Handle(DeletePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await priceLists.GetForUpdateAsync(request.Id, cancellationToken);
        priceLists.Remove(priceList);
        await priceLists.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
