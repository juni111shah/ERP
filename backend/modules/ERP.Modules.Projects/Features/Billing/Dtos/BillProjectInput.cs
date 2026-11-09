namespace ERP.Modules.Projects.Features;

/// <summary>
/// What to put on the invoice. Grouping: "detailed" (a line per entry), "person", "task" or "project" (one line each). DiscountPercent adds a
/// discount line. ProgressPercent (fixed-fee projects only) bills that share of the fee without a milestone.
/// A list of ids chooses those lines only: left out means everything ready to bill, an empty list means none of that kind.
/// </summary>
public sealed record BillProjectInput(
    Guid ProjectId,
    IReadOnlyList<Guid>? TimesheetIds = null,
    IReadOnlyList<Guid>? MilestoneIds = null,
    IReadOnlyList<Guid>? ExpenseIds = null,
    string Grouping = "detailed",
    decimal DiscountPercent = 0m,
    decimal? ProgressPercent = null,
    string? Notes = null,
    DateOnly? DueDate = null);
