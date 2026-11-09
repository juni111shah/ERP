using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class LogTimeEntryHandler(ITimeEntryRecordRepository time, IServiceEngagementRepository engagements, ILegalEntityGuard guard)
    : ICommandHandler<LogTimeEntryCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(LogTimeEntryCommand request, CancellationToken cancellationToken)
    {
        // 1. The engagement must be of this legal entity and still take time.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var engagement = await engagements.FindForUpdateAsync(request.EngagementId, cancellationToken)
            ?? throw new FieldValidationException("engagementId", "Engagement not found.");
        if (engagement.LegalEntityId != request.LegalEntityId)
            throw new FieldValidationException("engagementId", "The engagement belongs to another legal entity.");
        TimeEntryRules.EnsureTakesTime(engagement);

        // 2. Build the entry.
        var totalAmount = Math.Round(request.Hours * request.HourlyRate, 2);
        var entry = new TimeEntryRecord
        {
            LegalEntityId = request.LegalEntityId,
            EngagementId = request.EngagementId,
            ConsultantUserId = request.ConsultantUserId,
            ConsultantName = request.ConsultantName.Trim(),
            ActivityDate = request.ActivityDate,
            Hours = request.Hours,
            HourlyRate = request.HourlyRate,
            TotalAmount = totalAmount,
            TaskDescription = request.TaskDescription.Trim(),
            IsBillable = request.IsBillable,
            IsApproved = false,
            CustomFields = request.CustomFields
        };

        // 3. Add it to the engagement's consumption and save.
        time.Add(entry);
        engagement.ConsumedHours += request.Hours;
        engagement.ConsumedCost += totalAmount;

        await time.SaveChangesAsync(cancellationToken);
        return TimeEntryDto.From(entry);
    }
}
