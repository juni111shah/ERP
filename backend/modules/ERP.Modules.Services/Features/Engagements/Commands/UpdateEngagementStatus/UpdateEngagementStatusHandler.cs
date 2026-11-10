using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class UpdateEngagementStatusHandler(IServiceEngagementRepository engagements) : ICommandHandler<UpdateEngagementStatusCommand, EngagementDto>
{
    public async Task<EngagementDto> Handle(UpdateEngagementStatusCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and check the move is allowed.
        var engagement = await engagements.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Service engagement {request.Id} not found.");

        ServicesTransitions.Engagement.Ensure(engagement.Status, request.Status, isSystem: false);

        // 2. Move it; completing stamps the end date once.
        engagement.Status = request.Status;
        if (request.Status == EngagementStatus.Completed && engagement.ActualEndDate is null)
        {
            engagement.ActualEndDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        }

        // 3. Save.
        await engagements.SaveChangesAsync(cancellationToken);
        return EngagementDto.From(engagement);
    }
}
