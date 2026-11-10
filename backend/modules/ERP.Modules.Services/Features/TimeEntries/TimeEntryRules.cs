using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Checks shared by the time entry handlers; no I/O.</summary>
internal static class TimeEntryRules
{
    public static void EnsureTakesTime(ServiceEngagement engagement)
    {
        if (engagement.Status == EngagementStatus.Cancelled)
            throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled and takes no more time.");
    }

    /// <summary>The entry, when it exists and is not approved yet; <paramref name="action"/> says what was refused.</summary>
    public static TimeEntryRecord Unapproved(TimeEntryRecord? entry, Guid id, string action)
    {
        if (entry is null) throw new NotFoundException($"Time entry {id} not found.");
        if (entry.IsApproved) throw new ConflictException($"Approved time cannot be {action}.");
        return entry;
    }
}
