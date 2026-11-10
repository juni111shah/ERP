using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Whether a promotion code can be used today on an order of this total, and what it takes off.</summary>
public record ValidatePromoCodeQuery(
    Guid LegalEntityId,
    string PromoCode,
    decimal OrderTotal) : IQuery<PromoValidationResultDto>;
