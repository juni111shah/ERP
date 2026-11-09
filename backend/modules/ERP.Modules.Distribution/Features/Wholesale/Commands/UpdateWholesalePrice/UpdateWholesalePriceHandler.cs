using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class UpdateWholesalePriceHandler(IWholesalePriceMatrixRepository matrix) : ICommandHandler<UpdateWholesalePriceCommand, WholesalePriceMatrixDto>
{
    public async Task<WholesalePriceMatrixDto> Handle(UpdateWholesalePriceCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and apply the changes.
        var price = await matrix.GetForUpdateAsync(request.Id, cancellationToken);

        price.Tier = request.Tier;
        price.MinQuantity = request.MinQuantity;
        price.MaxQuantity = request.MaxQuantity;
        price.DiscountPercentage = request.DiscountPercentage;
        price.WholesaleUnitPrice = request.WholesaleUnitPrice;
        price.IsActive = request.IsActive;
        price.Notes = request.Notes?.Trim();

        // 2. Save.
        await matrix.SaveChangesAsync(cancellationToken);
        return WholesalePriceMatrixDto.From(price);
    }
}
