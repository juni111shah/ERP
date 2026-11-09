using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Rules shared by the review handlers: the cycle must be open, the input is copied the same way, and the answer carries the employee and cycle names.</summary>
public sealed class ReviewRules(IReviewCycleRepository cycles, IEmployeeRepository employees)
{
    public async Task RequireOpenCycleAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await cycles.FindAsync(cycleId, ct) ?? throw new FieldValidationException("cycleId", "Review cycle not found.");
        if (cycle.Status != ReviewCycleStatus.Open) throw new ConflictException($"The review cycle '{cycle.Name}' is closed.");
    }

    public static void Map(ReviewInput i, PerformanceReview r) { r.ReviewerName = i.ReviewerName?.Trim(); r.OverallRating = i.OverallRating; r.Goals = i.Goals; r.Comments = i.Comments; }

    public static List<ReviewCompetency> Competencies(PerformanceReview r, ReviewInput i)
        => i.Competencies.Select(c => new ReviewCompetency { ReviewId = r.Id, Name = c.Name.Trim(), Score = c.Score, Notes = c.Notes }).ToList();

    public async Task<ReviewDto> ToDtoAsync(PerformanceReview r, CancellationToken ct)
    {
        var name = await employees.GetNameAsync(r.EmployeeId, ct);
        var cycle = await cycles.GetNameAsync(r.CycleId, ct);
        return new(r.Id, r.EmployeeId, name, r.CycleId, cycle, r.ReviewerName, r.OverallRating, r.Goals, r.Comments, r.Status, r.SubmittedAt, r.AcknowledgedAt,
            r.Competencies.Select(c => new CompetencyDto(c.Id, c.Name, c.Score, c.Notes)).ToList());
    }
}
