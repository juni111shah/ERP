using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class CreateWholesalePriceHandler(IWholesalePriceMatrixRepository matrix, ILegalEntityGuard guard) : ICommandHandler<CreateWholesalePriceCommand, WholesalePriceMatrixDto>
{
    public async Task<WholesalePriceMatrixDto> Handle(CreateWholesalePriceCommand request, CancellationToken cancellationToken)
    {
        // 1. Build the tier.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var price = new WholesalePriceMatrix
        {
            LegalEntityId = request.LegalEntityId,
            ItemId = request.ItemId,
            ItemCode = request.ItemCode.Trim(),
            ItemName = request.ItemName.Trim(),
            Tier = request.Tier,
            MinQuantity = request.MinQuantity,
            MaxQuantity = request.MaxQuantity,
            DiscountPercentage = request.DiscountPercentage,
            WholesaleUnitPrice = request.WholesaleUnitPrice,
            IsActive = request.IsActive,
            Notes = request.Notes?.Trim()
        };

        // 2. Save.
        matrix.Add(price);
        await matrix.SaveChangesAsync(cancellationToken);
        return WholesalePriceMatrixDto.From(price);
    }
}
