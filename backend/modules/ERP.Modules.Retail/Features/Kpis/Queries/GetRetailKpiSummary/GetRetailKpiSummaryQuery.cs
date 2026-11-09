using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>Store, price list and promotion counts, and how often promotions were redeemed.</summary>
public record GetRetailKpiSummaryQuery : IQuery<RetailKpiSummaryDto>;
