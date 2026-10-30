using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

public record WholesalePriceMatrixDto(
    Guid Id,
    Guid LegalEntityId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    WholesaleTier Tier,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercentage,
    decimal WholesaleUnitPrice,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CreateWholesalePriceCommand(
    Guid LegalEntityId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    WholesaleTier Tier,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercentage,
    decimal WholesaleUnitPrice,
    bool IsActive,
    string? Notes) : ICommand<WholesalePriceMatrixDto>;

public record UpdateWholesalePriceCommand(
    Guid Id,
    WholesaleTier Tier,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercentage,
    decimal WholesaleUnitPrice,
    bool IsActive,
    string? Notes) : ICommand<WholesalePriceMatrixDto>;

public record DeleteWholesalePriceCommand(Guid Id) : ICommand<Unit>;

public record GetWholesalePriceByIdQuery(Guid Id) : IQuery<WholesalePriceMatrixDto?>;

public record CalculateWholesaleRateQuery(
    Guid LegalEntityId,
    Guid ItemId,
    WholesaleTier Tier,
    decimal Quantity) : IQuery<WholesaleRateCalculationDto>;

public record WholesaleRateCalculationDto(
    Guid ItemId,
    WholesaleTier Tier,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal ExtendedTotal,
    bool TierApplied);

public class CreateWholesalePriceValidator : AbstractValidator<CreateWholesalePriceCommand>
{
    public CreateWholesalePriceValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.MinQuantity).GreaterThan(0);
        RuleFor(x => x.WholesaleUnitPrice).GreaterThan(0);
    }
}

public class WholesaleHandlers :
    IRequestHandler<CreateWholesalePriceCommand, WholesalePriceMatrixDto>,
    IRequestHandler<UpdateWholesalePriceCommand, WholesalePriceMatrixDto>,
    IRequestHandler<DeleteWholesalePriceCommand, Unit>,
    IRequestHandler<GetWholesalePriceByIdQuery, WholesalePriceMatrixDto?>,
    IRequestHandler<CalculateWholesaleRateQuery, WholesaleRateCalculationDto>
{
    private readonly IRepository<WholesalePriceMatrix> _matrix;

    public WholesaleHandlers(IRepository<WholesalePriceMatrix> matrix)
    {
        _matrix = matrix;
    }

    public async Task<WholesalePriceMatrixDto> Handle(CreateWholesalePriceCommand request, CancellationToken cancellationToken)
    {
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

        _matrix.Add(price);
        await _matrix.SaveChangesAsync(cancellationToken);
        return Map(price);
    }

    public async Task<WholesalePriceMatrixDto> Handle(UpdateWholesalePriceCommand request, CancellationToken cancellationToken)
    {
        var price = await _matrix.GetForUpdateAsync(request.Id, cancellationToken);

        price.Tier = request.Tier;
        price.MinQuantity = request.MinQuantity;
        price.MaxQuantity = request.MaxQuantity;
        price.DiscountPercentage = request.DiscountPercentage;
        price.WholesaleUnitPrice = request.WholesaleUnitPrice;
        price.IsActive = request.IsActive;
        price.Notes = request.Notes?.Trim();

        await _matrix.SaveChangesAsync(cancellationToken);
        return Map(price);
    }

    public async Task<Unit> Handle(DeleteWholesalePriceCommand request, CancellationToken cancellationToken)
    {
        var price = await _matrix.GetForUpdateAsync(request.Id, cancellationToken);
        _matrix.Remove(price);
        await _matrix.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<WholesalePriceMatrixDto?> Handle(GetWholesalePriceByIdQuery request, CancellationToken cancellationToken)
    {
        var price = await _matrix.FindAsync(request.Id, cancellationToken);
        return price is null ? null : Map(price);
    }

    public async Task<WholesaleRateCalculationDto> Handle(CalculateWholesaleRateQuery request, CancellationToken cancellationToken)
    {
        var matchingRule = await _matrix.Query(new Specification<WholesalePriceMatrix>(p =>
            p.LegalEntityId == request.LegalEntityId &&
            p.ItemId == request.ItemId &&
            p.Tier == request.Tier &&
            p.IsActive &&
            p.MinQuantity <= request.Quantity &&
            (!p.MaxQuantity.HasValue || p.MaxQuantity.Value >= request.Quantity)))
            .OrderByDescending(p => p.MinQuantity)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchingRule != null)
        {
            var total = Math.Round(matchingRule.WholesaleUnitPrice * request.Quantity, 2);
            return new WholesaleRateCalculationDto(
                request.ItemId,
                request.Tier,
                request.Quantity,
                matchingRule.WholesaleUnitPrice,
                matchingRule.DiscountPercentage,
                total,
                true);
        }

        return new WholesaleRateCalculationDto(
            request.ItemId,
            request.Tier,
            request.Quantity,
            0,
            0,
            0,
            false);
    }

    private static WholesalePriceMatrixDto Map(WholesalePriceMatrix p) => new(
        p.Id,
        p.LegalEntityId,
        p.ItemId,
        p.ItemCode,
        p.ItemName,
        p.Tier,
        p.MinQuantity,
        p.MaxQuantity,
        p.DiscountPercentage,
        p.WholesaleUnitPrice,
        p.IsActive,
        p.Notes,
        p.CreatedAt);
}
