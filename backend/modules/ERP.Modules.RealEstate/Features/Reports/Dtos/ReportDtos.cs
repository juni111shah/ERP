namespace ERP.Modules.RealEstate.Features;

public sealed record RentRollRow(
    Guid PropertyId, string Property, Guid UnitId, string Unit, string? UnitType, decimal AreaSqft, Guid LeaseId, string? LeaseNumber, string Tenant, DateOnly Start, DateOnly Expiry, int DaysLeft,
    decimal AnnualRent, decimal RentPerSqft, string VatStatus, decimal PdcsHeld, decimal Outstanding, Guid? TenantPartyId = null);
public sealed record RentRollDto(DateOnly AsOf, IReadOnlyList<RentRollRow> Rows, decimal TotalAnnualRent, decimal TotalOutstanding);

public sealed record OccupancyRow(Guid PropertyId, string Property, Guid LegalEntityId, int TotalUnits, int Registered, int Occupied, int Vacant, int Other, decimal OccupancyPercent, decimal ContractedRent, decimal VacantAskingRent);
public sealed record OccupancyDto(IReadOnlyList<OccupancyRow> Rows, decimal OccupancyPercent, decimal ContractedRent, decimal VacancyAskingRent, int VacantUnits);

public sealed record PerformanceRow(Guid PropertyId, string Property, Guid LegalEntityId, decimal Value, decimal OccupancyPercent, decimal Income, decimal OperatingExpenses, decimal Noi, decimal AnnualisedYieldPercent, decimal Billed, decimal Collected, decimal CollectionRatePercent);
public sealed record PerformanceDto(DateOnly From, DateOnly To, IReadOnlyList<PerformanceRow> Rows, PerformanceRow Total);

public sealed record ExpiryRow(string Label, DateOnly? Month, int Leases, decimal AnnualRentAtRisk, bool PastEnd);
public sealed record LeaseExpiriesDto(IReadOnlyList<ExpiryRow> Rows, int TotalLeases, decimal TotalRentAtRisk);

public sealed record MaintenanceRow(Guid PropertyId, string Property, string Category, int WorkOrders, int Open, decimal Estimated, decimal Actual);
public sealed record MaintenanceCostsDto(DateOnly From, DateOnly To, IReadOnlyList<MaintenanceRow> Rows, int WorkOrders, int Open, decimal Estimated, decimal Actual);

public sealed record CommissionRow(string Agent, int Deals, decimal DealValue, decimal GrossCommission, decimal AgentShare, decimal TeamLeadShare, decimal CompanyShare, decimal PaidOut);
public sealed record CommissionsDto(DateOnly From, DateOnly To, IReadOnlyList<CommissionRow> Rows, CommissionRow Total);
