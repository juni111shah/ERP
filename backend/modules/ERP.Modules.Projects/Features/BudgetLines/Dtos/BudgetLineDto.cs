namespace ERP.Modules.Projects.Features;

public sealed record BudgetLineInput(Guid ProjectId, string Category, decimal Amount, decimal Hours = 0m);

/// <summary>Id is null for spending in a category that has no budget line.</summary>
public sealed record BudgetLineDto(Guid? Id, string Category, decimal Amount, decimal Hours, decimal Actual, decimal ActualHours);
