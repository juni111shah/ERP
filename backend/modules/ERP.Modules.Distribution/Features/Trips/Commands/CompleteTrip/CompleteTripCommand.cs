using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Reconciles a returning trip: records what was sold, returned and damaged, issues the stock and optionally invoices the sales.</summary>
/// <param name="CustomerPartyId">When set, what was sold is invoiced to this customer at the trip's prices and the invoice is issued, posting revenue and the receivable.</param>
/// <param name="AllowUnaccounted">Every loaded unit must be sold, returned or damaged unless this is set.</param>
public record CompleteTripCommand(
    Guid TripId,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    List<RecordTripItemReconciliationInput> Items,
    string? Notes,
    Guid? CustomerPartyId = null,
    bool AllowUnaccounted = false) : ICommand<VanSalesTripDto>;
