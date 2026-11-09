using ERP.Application.Common;
using ERP.Modules.Services.Repositories;

namespace ERP.Modules.Services.Features;

internal sealed class UpdateTimeEntryHandler(ITimeEntryRecordRepository time, IServiceEngagementRepository engagements) : ICommandHandler<UpdateTimeEntryCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(UpdateTimeEntryCommand request, CancellationToken cancellationToken)
    {
        // 1. Only unapproved time on an engagement that still takes time can be corrected.
        var entry = TimeEntryRules.Unapproved(await time.FindForUpdateAsync(request.Id, cancellationToken), request.Id, "corrected");
        var engagement = await engagements.FirstForUpdateAsync(entry.EngagementId, cancellationToken);
        TimeEntryRules.EnsureTakesTime(engagement);

        // 2. Move the difference onto the engagement's consumption.
        var totalAmount = Math.Round(request.Hours * request.HourlyRate, 2);
        engagement.ConsumedHours += request.Hours - entry.Hours;
        engagement.ConsumedCost += totalAmount - entry.TotalAmount;

        // 3. Correct the entry and save.
        entry.ActivityDate = request.ActivityDate;
        entry.Hours = request.Hours;
        entry.HourlyRate = request.HourlyRate;
        entry.TotalAmount = totalAmount;
        entry.TaskDescription = request.TaskDescription.Trim();
        entry.IsBillable = request.IsBillable;
        entry.ConsultantUserId = request.ConsultantUserId ?? entry.ConsultantUserId;
        if (!string.IsNullOrWhiteSpace(request.ConsultantName)) entry.ConsultantName = request.ConsultantName.Trim();
        entry.CustomFields = request.CustomFields ?? entry.CustomFields;

        await time.SaveChangesAsync(cancellationToken);
        return TimeEntryDto.From(entry);
    }
}
