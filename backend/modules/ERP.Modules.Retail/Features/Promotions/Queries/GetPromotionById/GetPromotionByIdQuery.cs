using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>One promotion; null when it does not exist.</summary>
public record GetPromotionByIdQuery(Guid Id) : IQuery<RetailPromotionDto?>;
