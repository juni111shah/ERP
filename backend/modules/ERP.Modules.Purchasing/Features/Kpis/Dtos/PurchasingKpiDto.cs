namespace ERP.Modules.Purchasing.Features;

public sealed record PurchasingKpiDto(
    decimal TotalSpend,
    decimal OpenOrdersAmount,
    int OpenOrdersCount,
    int PendingReceiptsCount,
    int OpenRequisitionsCount,
    int TotalOrdersCount);
