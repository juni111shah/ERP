using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class AcknowledgeReviewHandler(IPerformanceReviewRepository reviews, ReviewRules rules, IClock clock) : ICommandHandler<AcknowledgeReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(AcknowledgeReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetWithCompetenciesForUpdateAsync(command.Id, ct);
        if (review.Status != ReviewStatus.Submitted) throw new ConflictException(review.Status == ReviewStatus.Draft ? "Submit the review first." : "This review is already acknowledged.");
        review.Status = ReviewStatus.Acknowledged; review.AcknowledgedAt = clock.UtcNow;
        await reviews.SaveChangesAsync(ct);
        return await rules.ToDtoAsync(review, ct);
    }
}
