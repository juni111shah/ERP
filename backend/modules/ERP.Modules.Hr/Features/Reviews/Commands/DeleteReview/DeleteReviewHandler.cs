using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteReviewHandler(IPerformanceReviewRepository reviews) : ICommandHandler<DeleteReviewCommand>
{
    public async Task<Unit> Handle(DeleteReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetForUpdateAsync(command.Id, ct);
        if (review.Status != ReviewStatus.Draft) throw new ConflictException("Only a draft review can be deleted.");
        reviews.Remove(review);
        await reviews.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
