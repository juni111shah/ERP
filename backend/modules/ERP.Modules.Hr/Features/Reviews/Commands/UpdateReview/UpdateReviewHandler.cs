using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateReviewHandler(IPerformanceReviewRepository reviews, IRepository<ReviewCompetency> competencyRows, ReviewRules rules) : ICommandHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand command, CancellationToken ct)
    {
        // 1. Only a draft, for the same employee and cycle, in an open cycle.
        var review = await reviews.GetWithCompetenciesForUpdateAsync(command.Id, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("Only a draft review can be edited.");
        var i = command.Input;
        if (i.EmployeeId != review.EmployeeId || i.CycleId != review.CycleId) throw new FieldValidationException("employeeId", "The employee and cycle of a review cannot change.");
        await rules.RequireOpenCycleAsync(review.CycleId, ct);

        // 2. Apply, replace the competencies, and save.
        ReviewRules.Map(i, review);
        // New children with client-side GUIDs go through their own repository (see journals): adding through the navigation alone is read as an update.
        foreach (var old in review.Competencies.ToList()) competencyRows.Remove(old);
        var fresh = ReviewRules.Competencies(review, i);
        competencyRows.AddRange(fresh);
        await reviews.SaveChangesAsync(ct);
        review.Competencies.Clear();
        review.Competencies.AddRange(fresh);
        return await rules.ToDtoAsync(review, ct);
    }
}
