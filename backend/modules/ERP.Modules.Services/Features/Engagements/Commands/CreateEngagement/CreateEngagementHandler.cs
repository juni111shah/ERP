using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class CreateEngagementHandler(
    IServiceEngagementRepository engagements,
    INumberingService numbering,
    ClientReferences clients,
    ILegalEntityGuard guard,
    IUserDirectory users)
    : ICommandHandler<CreateEngagementCommand, EngagementDto>
{
    public async Task<EngagementDto> Handle(CreateEngagementCommand request, CancellationToken cancellationToken)
    {
        // 1. The client must be a customer; a lead consultant must be a member of the workspace.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var clientName = await clients.RequireNameAsync(request.ClientId, cancellationToken);
        if (request.LeadConsultantUserId is { } lead && (await users.ListMembersAsync([lead], cancellationToken)).Count == 0)
            throw new FieldValidationException("leadConsultantUserId", "That user is not a member of this workspace.");

        // 2. Number and build it.
        var code = await numbering.NextAsync("ENG", request.LegalEntityId, request.StartDate, cancellationToken);
        var engagement = new ServiceEngagement
        {
            LegalEntityId = request.LegalEntityId,
            EngagementCode = code,
            Title = request.Title.Trim(),
            ClientId = request.ClientId,
            ClientName = clientName,
            Type = request.Type,
            Status = EngagementStatus.Active,
            ContractValue = request.ContractValue,
            BudgetedHours = request.BudgetedHours,
            ConsumedHours = 0,
            ConsumedCost = 0,
            StartDate = request.StartDate,
            TargetEndDate = request.TargetEndDate,
            LeadConsultantUserId = request.LeadConsultantUserId,
            LeadConsultantName = request.LeadConsultantName?.Trim(),
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields
        };

        // 3. Save.
        engagements.Add(engagement);
        await engagements.SaveChangesAsync(cancellationToken);

        return EngagementDto.From(engagement);
    }
}
