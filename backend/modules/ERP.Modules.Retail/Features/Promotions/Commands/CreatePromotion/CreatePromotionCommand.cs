using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

/// <summary>Creates a promotion with an upper-cased code.</summary>
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
