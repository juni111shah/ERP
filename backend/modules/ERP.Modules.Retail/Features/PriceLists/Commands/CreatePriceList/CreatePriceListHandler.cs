using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class CreatePriceListHandler(IRetailPriceListRepository priceLists, PriceListCurrencies currencies, ILegalEntityGuard guard)
    : ICommandHandler<CreatePriceListCommand, RetailPriceListDto>
{
    public async Task<RetailPriceListDto> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
    {
        // 1. The code is unique.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var code = request.Code.Trim();
        var exists = await priceLists.CodeExistsAsync(code, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Price list code '{code}' already exists.");
        }

        // 2. Build the list with its rows.
        var priceList = new RetailPriceList
        {
            LegalEntityId = request.LegalEntityId,
            Code = code,
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = await currencies.CurrencyAsync(request.Currency, request.LegalEntityId, cancellationToken),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields,
            Items = request.Items.Select(i => new RetailPriceListItem
            {
                ItemId = i.ItemId,
                ItemCode = i.ItemCode.Trim(),
                ItemName = i.ItemName.Trim(),
                BasePrice = i.BasePrice,
                Price = i.Price,
                MinQuantity = i.MinQuantity
            }).ToList()
        };

        // 3. Save.
        priceLists.Add(priceList);
        await priceLists.SaveChangesAsync(cancellationToken);
        return RetailPriceListDto.From(priceList);
    }
}
