namespace ERP.Modules.RealEstate.Features;

public enum RenewalState { Due, DraftRenewal, Renewed }

public sealed record RenewalRow(Guid LeaseId, string? Number, string Tenant, string Property, string Unit, DateOnly EndDate, int DaysLeft, decimal AnnualRent, decimal MarketRent, decimal? MarketVariancePercent, string CurrencyCode, RenewalState State, Guid? RenewalLeaseId);
public sealed record RenewalWorkbenchDto(DateOnly AsOf, int WithinDays, IReadOnlyList<RenewalRow> Rows, int Due, int Draft, int Renewed);
public sealed record RenewalDraftResult(Guid LeaseId, Guid? RenewalLeaseId, string? Error);
