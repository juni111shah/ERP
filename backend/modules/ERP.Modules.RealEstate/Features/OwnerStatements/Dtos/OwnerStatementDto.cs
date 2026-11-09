namespace ERP.Modules.RealEstate.Features;

public sealed record OwnerStatementRow(Guid PropertyId, string Property, decimal FeePercent, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner, decimal ChargedToTenant = 0, decimal TaxCollected = 0);
/// <summary>One maintenance job in the period: what it cost, where the cost figure came from (bill, actual, estimate) and who bears it.</summary>
public sealed record OwnerStatementLine(Guid WorkOrderId, string? Number, string Property, string Issue, DateOnly? CompletedOn, decimal Cost, string CostSource, string BorneBy, decimal OwnerAmount, decimal TenantAmount);
/// <summary>RentCollected is net of tax; TaxCollected is the tax in the same receipts, which stays with the company for the tax return. PaidToOwner is what live payouts overlapping the period paid.</summary>
public sealed record OwnerStatementDto(Guid OwnerPartyId, string Owner, DateOnly From, DateOnly To, IReadOnlyList<OwnerStatementRow> Rows, decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal NetToOwner,
    IReadOnlyList<OwnerStatementLine>? Lines = null, decimal ChargedToTenant = 0, decimal TaxCollected = 0, decimal PaidToOwner = 0);
