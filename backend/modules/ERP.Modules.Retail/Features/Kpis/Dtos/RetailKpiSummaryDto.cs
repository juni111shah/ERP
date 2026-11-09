namespace ERP.Modules.Retail.Features;

public record RetailKpiSummaryDto(
    int TotalStoresCount,
    int ActiveStoresCount,
    int ActivePriceListsCount,
    int ActivePromotionsCount,
    int TotalPromotionsRedeemed);
