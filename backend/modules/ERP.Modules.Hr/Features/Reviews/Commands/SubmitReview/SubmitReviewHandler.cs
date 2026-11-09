using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class SubmitReviewHandler(IPerformanceReviewRepository reviews, ReviewRules rules, IClock clock) : ICommandHandler<SubmitReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(SubmitReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetWithCompetenciesForUpdateAsync(command.Id, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("This review has already been submitted.");
        if (review.OverallRating is null) throw new ConflictException("Give an overall rating before submitting.");
        review.Status = ReviewStatus.Submitted; review.SubmittedAt = clock.UtcNow;
        await reviews.SaveChangesAsync(ct);
        return await rules.ToDtoAsync(review, ct);
    }
}
