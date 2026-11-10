using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record ProfitabilityRow(Guid ProjectId, string Number, string Name, string? CustomerName, ProjectStatus Status, ProjectBillingType BillingType,
    decimal Hours, decimal BillableHours, decimal Billed, decimal Unbilled, decimal LabourCost, decimal ExpenseCost, decimal Cost, decimal Margin, decimal MarginPercent,
    decimal ContractValue = 0m, decimal? ContractMargin = null, decimal? ContractMarginPercent = null);
public sealed record UtilisationRow(string Person, Guid? UserId, decimal LoggedHours, decimal BillableHours, decimal CapacityHours, decimal AllocatedHours, decimal UtilisationPercent, decimal BillablePercent);
public sealed record BudgetRow(Guid ProjectId, string Number, string Name, ProjectStatus Status, ProjectBillingType BillingType, decimal Budget, decimal ApprovedChanges, decimal Cost, decimal UsedPercent,
    decimal BudgetHours, decimal LoggedHours, decimal PercentComplete, decimal ForecastCost, decimal ForecastVariance, decimal EarnedRevenue, decimal Billed, decimal WorkInProgress, string Health,
    decimal PlannedValue = 0m, decimal EarnedValue = 0m, decimal? CostIndex = null, decimal? ScheduleIndex = null, decimal ContractValue = 0m, decimal? ForecastMargin = null);
public sealed record AgeingRow(Guid ProjectId, string Number, string Name, decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);
public sealed record HoursRow(string Group, decimal Hours, decimal BillableHours, decimal Amount, decimal Cost);
