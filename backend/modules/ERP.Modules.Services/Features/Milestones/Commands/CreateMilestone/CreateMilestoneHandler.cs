using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class CreateMilestoneHandler(
    IBillingMilestoneRepository milestones,
    IServiceEngagementRepository engagements,
    INumberingService numbering,
    ERP.Application.Organizations.ILegalEntityGuard guard)
    : ICommandHandler<CreateMilestoneCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(CreateMilestoneCommand request, CancellationToken cancellationToken)
    {
        // 1. The engagement must be of this legal entity, not cancelled, and have room in its contract value.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var engagement = await engagements.FindAsync(request.EngagementId, cancellationToken)
            ?? throw new FieldValidationException("engagementId", "Engagement not found.");
        if (engagement.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("engagementId", "The engagement belongs to another legal entity.");
        if (engagement.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled.");
        await EnsureWithinContractAsync(engagement, request.Amount, cancellationToken);

        // 2. Number and build it.
        var code = await numbering.NextAsync("MLS", request.LegalEntityId, request.DueDate, cancellationToken);
        var milestone = new BillingMilestone
        {
            LegalEntityId = request.LegalEntityId,
            MilestoneCode = code,
            EngagementId = request.EngagementId,
            Title = request.Title.Trim(),
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = MilestoneStatus.Planned,
            Notes = request.Notes?.Trim(),
            CustomFields = request.CustomFields
        };

        // 3. Save.
        milestones.Add(milestone);
        await milestones.SaveChangesAsync(cancellationToken);

        return MilestoneDto.From(milestone);
    }

    /// <summary>The milestones of an engagement together cannot ask for more than its contract value. A contract value of zero sets no cap.</summary>
    private async Task EnsureWithinContractAsync(ServiceEngagement engagement, decimal amount, CancellationToken ct)
    {
        if (engagement.ContractValue <= 0) return;
        var planned = await milestones.TotalForEngagementAsync(engagement.Id, ct);
        if (planned + amount > engagement.ContractValue)
            throw new FieldValidationException("amount", $"The milestones would add up to {planned + amount:N2}, more than the contract value of {engagement.ContractValue:N2}.");
    }
}
