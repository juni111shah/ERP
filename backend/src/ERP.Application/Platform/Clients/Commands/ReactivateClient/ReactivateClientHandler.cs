using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ReactivateClientHandler(ClientSubscriptions clients, ISubscriptionInvoiceRepository invoices, PlatformLog log, IPlatformMailer mailer, IClock clock)
    : ICommandHandler<ReactivateClientCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(ReactivateClientCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        if (s.Status is not (SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)) throw new ConflictException("This client is not suspended or cancelled.");
        SubscriptionRules.Reactivate(s, await invoices.HasOverdueAsync(command.OrgId, null, DateOnly.FromDateTime(clock.UtcNow), ct), DateOnly.FromDateTime(clock.UtcNow));
        log.Add("Reactivated", command.OrgId, org.Name, $"Now {SubscriptionRules.StatusWords(s.Status)}.");
        var detail = await clients.SavedAsync(command.OrgId, ct);
        await mailer.SendAsync(s.BillingEmail, "Your workspace is back", $"{org.Name} is available again: {mailer.Link("/auth/login")}", ct);
        return detail;
    }
}
