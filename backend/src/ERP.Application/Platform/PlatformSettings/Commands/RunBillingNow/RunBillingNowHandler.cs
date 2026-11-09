using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class RunBillingNowHandler(ISubscriptionLifecycle lifecycle, IRepository<PlatformSetting> settings, PlatformLog log, IClock clock) : ICommandHandler<RunBillingNowCommand, LifecycleResult>
{
    public async Task<LifecycleResult> Handle(RunBillingNowCommand command, CancellationToken ct)
    {
        var result = await lifecycle.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct);
        log.Add("Billing run", null, null, $"{result.TrialsExpired} trials ended, {result.InvoicesIssued} invoices issued, {result.MarkedPastDue} overdue, {result.Suspended} suspended.");
        await settings.SaveChangesAsync(ct);
        return result;
    }
}
