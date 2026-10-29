using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

public record RetailPriceListItemInput(
    Guid? ItemId,
    string ItemCode,
    string ItemName,
    decimal BasePrice,
    decimal Price,
    decimal? MinQuantity);

public record RetailPriceListItemDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string ItemName,
    decimal BasePrice,
    decimal Price,
    decimal? MinQuantity);

public record RetailPriceListSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    int ItemCount,
    DateTimeOffset CreatedAt);

public record RetailPriceListDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    string? Description,
    string? CustomFields,
    List<RetailPriceListItemDto> Items,
    DateTimeOffset CreatedAt);

public record CreatePriceListCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    string? Description,
    string? CustomFields,
    List<RetailPriceListItemInput> Items) : ICommand<RetailPriceListDto>;

public record UpdatePriceListCommand(
    Guid Id,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    string? Description,
    string? CustomFields,
    List<RetailPriceListItemInput> Items) : ICommand<RetailPriceListDto>;

public record DeletePriceListCommand(Guid Id) : ICommand<Unit>;

public record GetPriceListByIdQuery(Guid Id) : IQuery<RetailPriceListDto>;

public class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
    }
}

public class UpdatePriceListValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class PriceListHandlers :
    IRequestHandler<CreatePriceListCommand, RetailPriceListDto>,
    IRequestHandler<UpdatePriceListCommand, RetailPriceListDto>,
    IRequestHandler<DeletePriceListCommand, Unit>,
    IRequestHandler<GetPriceListByIdQuery, RetailPriceListDto>
{
    private readonly IRepository<RetailPriceList> _priceLists;

    public PriceListHandlers(IRepository<RetailPriceList> priceLists)
    {
        _priceLists = priceLists;
    }

    public async Task<RetailPriceListDto> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = new RetailPriceList
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = request.Currency.Trim().ToUpperInvariant(),
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

        _priceLists.Add(priceList);
        await _priceLists.SaveChangesAsync(cancellationToken);
        return Map(priceList);
    }

    public async Task<RetailPriceListDto> Handle(UpdatePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _priceLists.Tracked(new Specification<RetailPriceList>(x => x.Id == request.Id))
            .Include(x => x.Items)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"PriceList {request.Id} not found");

        priceList.Name = request.Name.Trim();
        priceList.Type = request.Type;
        priceList.Currency = request.Currency.Trim().ToUpperInvariant();
        priceList.ValidFrom = request.ValidFrom;
        priceList.ValidTo = request.ValidTo;
        priceList.IsDefault = request.IsDefault;
        priceList.IsActive = request.IsActive;
        priceList.Description = request.Description?.Trim();
        priceList.CustomFields = request.CustomFields;

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

        await _priceLists.SaveChangesAsync(cancellationToken);
        return Map(priceList);
    }

    public async Task<Unit> Handle(DeletePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _priceLists.GetForUpdateAsync(request.Id, cancellationToken);
        _priceLists.Remove(priceList);
        await _priceLists.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<RetailPriceListDto> Handle(GetPriceListByIdQuery request, CancellationToken cancellationToken)
    {
        var priceList = await _priceLists.Query(new Specification<RetailPriceList>(x => x.Id == request.Id))
            .Include(x => x.Items)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"PriceList {request.Id} not found");

        return Map(priceList);
    }

    private static RetailPriceListDto Map(RetailPriceList p) => new(
        p.Id,
        p.LegalEntityId,
        p.Code,
        p.Name,
        p.Type,
        p.Currency,
        p.ValidFrom,
        p.ValidTo,
        p.IsDefault,
        p.IsActive,
        p.Description,
        p.CustomFields,
        p.Items.Select(i => new RetailPriceListItemDto(
            i.Id,
            i.ItemId,
            i.ItemCode,
            i.ItemName,
            i.BasePrice,
            i.Price,
            i.MinQuantity)).ToList(),
        p.CreatedAt);
}
