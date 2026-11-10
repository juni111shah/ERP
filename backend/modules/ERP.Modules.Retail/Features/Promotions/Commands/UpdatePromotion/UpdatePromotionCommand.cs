using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

/// <summary>Updates a promotion; its code stays.</summary>
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
