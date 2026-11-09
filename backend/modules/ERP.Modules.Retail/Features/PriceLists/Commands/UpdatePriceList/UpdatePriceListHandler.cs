using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class UpdatePriceListHandler(IRetailPriceListRepository priceLists, PriceListCurrencies currencies)
    : ICommandHandler<UpdatePriceListCommand, RetailPriceListDto>
{
    public async Task<RetailPriceListDto> Handle(UpdatePriceListCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and apply the changes.
        var priceList = await priceLists.GetWithItemsForUpdateAsync(request.Id, cancellationToken);

        priceList.Name = request.Name.Trim();
        priceList.Type = request.Type;
        priceList.Currency = await currencies.CurrencyAsync(request.Currency, priceList.LegalEntityId, cancellationToken);
        priceList.ValidFrom = request.ValidFrom;
        priceList.ValidTo = request.ValidTo;
        priceList.IsDefault = request.IsDefault;
        priceList.IsActive = request.IsActive;
        priceList.Description = request.Description?.Trim();
        priceList.CustomFields = request.CustomFields;

        // 2. The rows sent replace the current ones.
        priceList.Items.Clear();
        foreach (var i in request.Items)
        {
            priceList.Items.Add(new RetailPriceListItem
            {
                PriceListId = priceList.Id,
                ItemId = i.ItemId,
                ItemCode = i.ItemCode.Trim(),
                ItemName = i.ItemName.Trim(),
                BasePrice = i.BasePrice,
                Price = i.Price,
                MinQuantity = i.MinQuantity
            });
        }

        // 3. Save.
        await priceLists.SaveChangesAsync(cancellationToken);
        return RetailPriceListDto.From(priceList);
    }
}
