using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record ReviewCycleInput(string Name, DateOnly PeriodStart, DateOnly PeriodEnd, ReviewCycleStatus Status = ReviewCycleStatus.Open);
public sealed record ReviewCycleDto(Guid Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, ReviewCycleStatus Status)
{
    public static ReviewCycleDto From(ReviewCycle c) => new(c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<ReviewCycle, ReviewCycleDto>> Projection = c => new ReviewCycleDto(c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status);
}

public sealed record CompetencyInput(string Name, int Score, string? Notes = null);
public sealed record ReviewInput(Guid EmployeeId, Guid CycleId, string? ReviewerName, int? OverallRating, string? Goals, string? Comments, IReadOnlyList<CompetencyInput> Competencies);
public sealed record CompetencyDto(Guid Id, string Name, int Score, string? Notes);
public sealed record ReviewDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid CycleId, string CycleName, string? ReviewerName, int? OverallRating, string? Goals, string? Comments, ReviewStatus Status,
    DateTime? SubmittedAt, DateTime? AcknowledgedAt, IReadOnlyList<CompetencyDto> Competencies);
public sealed record ReviewRow(Guid Id, Guid EmployeeId, Guid CycleId, string? ReviewerName, int? OverallRating, ReviewStatus Status, DateTime? SubmittedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PerformanceReview, ReviewRow>> Projection = r => new ReviewRow(r.Id, r.EmployeeId, r.CycleId, r.ReviewerName, r.OverallRating, r.Status, r.SubmittedAt);
}
