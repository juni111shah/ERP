using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class CreateRetainerHandler(
    IRetainerContractRepository retainers,
    INumberingService numbering,
    IServiceEngagementRepository engagements,
    ClientReferences clients,
    ILegalEntityGuard guard)
    : ICommandHandler<CreateRetainerCommand, RetainerDto>
{
    public async Task<RetainerDto> Handle(CreateRetainerCommand request, CancellationToken cancellationToken)
    {
        // 1. The engagement must be of this legal entity, not cancelled, and for the same client.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var engagement = await engagements.FindAsync(request.EngagementId, cancellationToken)
            ?? throw new FieldValidationException("engagementId", "Engagement not found.");
        if (engagement.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("engagementId", "The engagement belongs to another legal entity.");
        if (engagement.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled.");
        if (engagement.ClientId != request.ClientId) throw new FieldValidationException("clientId", "The client must be the client of the engagement.");
        var clientName = await clients.RequireNameAsync(request.ClientId, cancellationToken);
        if (request.EndDate is { } end && end < request.StartDate) throw new FieldValidationException("endDate", "The end date cannot be before the start date.");

        // 2. Number and build it.
        var number = await numbering.NextAsync("RET", request.LegalEntityId, request.StartDate, cancellationToken);
        var contract = new RetainerContract
        {
            LegalEntityId = request.LegalEntityId,
            RetainerNumber = number,
            EngagementId = request.EngagementId,
            ClientId = request.ClientId,
            ClientName = clientName,
            Title = request.Title.Trim(),
            Cycle = request.Cycle,
            Status = RetainerStatus.Active,
            RecurringFee = request.RecurringFee,
            IncludedHours = request.IncludedHours,
            OverageHourlyRate = request.OverageHourlyRate,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Terms = request.Terms?.Trim(),
            CustomFields = request.CustomFields
        };

        // 3. Save.
        retainers.Add(contract);
        await retainers.SaveChangesAsync(cancellationToken);

        return RetainerDto.From(contract);
    }
}
