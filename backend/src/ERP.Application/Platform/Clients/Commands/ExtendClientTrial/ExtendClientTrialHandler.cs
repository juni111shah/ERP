using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ExtendClientTrialHandler(ClientSubscriptions clients, PlatformLog log, IClock clock) : ICommandHandler<ExtendClientTrialCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(ExtendClientTrialCommand command, CancellationToken ct)
    {
        if (command.Days is < 1 or > 365) throw new FieldValidationException("days", "Use 1 to 365 days.");
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        if (s.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Cancelled) throw new ConflictException("Only a trial, or a workspace whose trial has lapsed, can get more trial days.");
        if (s.Status == SubscriptionStatus.Suspended && s.SuspendedByFounder) throw new ConflictException("Reactivate this client first.");
        var from = s.TrialEndsOn is { } end && end > today ? end : today;
        s.TrialEndsOn = from.AddDays(command.Days);
        s.Status = SubscriptionStatus.Trialing;
        s.LapsedOn = null; s.SuspendedAt = null; s.SuspendedReason = null; s.LastReminderKey = null;
        log.Add("Trial extended", command.OrgId, org.Name, $"{command.Days} more days, to {s.TrialEndsOn:d MMM yyyy}.");
        return await clients.SavedAsync(command.OrgId, ct);
    }
}
