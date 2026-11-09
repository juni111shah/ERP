namespace ERP.Modules.Services.Features;

public record ServicesKpisDto(
    int ActiveEngagements,
    decimal TotalContractValue,
    decimal RetainerMonthlyRecurringRevenue,
    decimal PendingMilestonesValue,
    decimal ReadyToInvoiceMilestonesValue,
    decimal TotalLoggedHours,
    decimal BillableHours,
    decimal BillableUtilizationRate);
