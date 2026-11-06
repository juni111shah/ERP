using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Retail.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

public record RetailPromotionDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PromotionType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit,
    int TimesUsed,
    bool IsActive,
    string? Description,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record CreatePromotionCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    PromotionType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit,
    bool IsActive,
    string? Description,
    string? CustomFields) : ICommand<RetailPromotionDto>;

public record UpdatePromotionCommand(
    Guid Id,
    string Name,
    PromotionType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit,
    bool IsActive,
    string? Description,
    string? CustomFields) : ICommand<RetailPromotionDto>;

public record DeletePromotionCommand(Guid Id) : ICommand<Unit>;

public record GetPromotionByIdQuery(Guid Id) : IQuery<RetailPromotionDto?>;

public record ValidatePromoCodeQuery(
    Guid LegalEntityId,
    string PromoCode,
    decimal OrderTotal) : IQuery<PromoValidationResultDto>;

public record PromoValidationResultDto(
    bool IsValid,
    string? Message,
    PromotionType? Type,
    decimal DiscountValue,
    decimal CalculatedDiscount);

public class CreatePromotionValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public class UpdatePromotionValidator : AbstractValidator<UpdatePromotionCommand>
{
    public UpdatePromotionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountValue).GreaterThan(0);
    }
}

public class PromotionHandlers :
    IRequestHandler<CreatePromotionCommand, RetailPromotionDto>,
    IRequestHandler<UpdatePromotionCommand, RetailPromotionDto>,
    IRequestHandler<DeletePromotionCommand, Unit>,
    IRequestHandler<ValidatePromoCodeQuery, PromoValidationResultDto>,
    IRequestHandler<GetPromotionByIdQuery, RetailPromotionDto?>
{
    private readonly IRepository<RetailPromotion> _promotions;
    private readonly ILegalEntityGuard _guard;

    public PromotionHandlers(IRepository<RetailPromotion> promotions, ILegalEntityGuard guard)
    {
        _promotions = promotions;
        _guard = guard;
    }

    public async Task<RetailPromotionDto?> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.FindAsync(request.Id, cancellationToken);
        return promo is null ? null : Map(promo);
    }

    public async Task<RetailPromotionDto> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = new RetailPromotion
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Type = request.Type,
            DiscountValue = request.DiscountValue,
            MinimumOrderAmount = request.MinimumOrderAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            UsageLimit = request.UsageLimit,
            TimesUsed = 0,
            IsActive = request.IsActive,
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields
        };

        _promotions.Add(promo);
        await _promotions.SaveChangesAsync(cancellationToken);
        return Map(promo);
    }

    public async Task<RetailPromotionDto> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetForUpdateAsync(request.Id, cancellationToken);

        promo.Name = request.Name.Trim();
        promo.Type = request.Type;
        promo.DiscountValue = request.DiscountValue;
        promo.MinimumOrderAmount = request.MinimumOrderAmount;
        promo.StartDate = request.StartDate;
        promo.EndDate = request.EndDate;
        promo.UsageLimit = request.UsageLimit;
        promo.IsActive = request.IsActive;
        promo.Description = request.Description?.Trim();
        promo.CustomFields = request.CustomFields;

        await _promotions.SaveChangesAsync(cancellationToken);
        return Map(promo);
    }

    public async Task<Unit> Handle(DeletePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetForUpdateAsync(request.Id, cancellationToken);
        _promotions.Remove(promo);
        await _promotions.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<PromoValidationResultDto> Handle(ValidatePromoCodeQuery request, CancellationToken cancellationToken)
    {
        var code = request.PromoCode.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);

        var promo = await _promotions.Query(new Specification<RetailPromotion>(p =>
            p.Code == code && p.LegalEntityId == request.LegalEntityId))
            .FirstOrDefaultAsync(cancellationToken);

        var currency = (await _guard.RequireAsync(request.LegalEntityId, cancellationToken)).BaseCurrency;
        var problem = PromotionRules.Problem(promo, today, request.OrderTotal, currency);
        if (problem is not null)
        {
            return new PromoValidationResultDto(false, problem, null, 0, 0);
        }

        return new PromoValidationResultDto(true, "Promotion applied successfully.", promo!.Type, promo.DiscountValue, PromotionRules.Discount(promo, request.OrderTotal));
    }

    private static RetailPromotionDto Map(RetailPromotion p) => new(
        p.Id,
        p.LegalEntityId,
        p.Code,
        p.Name,
        p.Type,
        p.DiscountValue,
        p.MinimumOrderAmount,
        p.StartDate,
        p.EndDate,
        p.UsageLimit,
        p.TimesUsed,
        p.IsActive,
        p.Description,
        p.CustomFields,
        p.CreatedAt);
}
