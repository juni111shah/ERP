using ERP.Application.Abstractions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Runs every <see cref="IScheduledJob"/> for every workspace, then sleeps. A failing job is logged and never stops the others.</summary>
public sealed class ScheduledJobRunner(IServiceScopeFactory scopes, ILogger<ScheduledJobRunner> log, TimeSpan interval, TimeSpan firstDelay) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        try { await Task.Delay(firstDelay, stop); } catch (OperationCanceledException) { return; }
        while (!stop.IsCancellationRequested)
        {
            await RunOnceAsync(DateOnly.FromDateTime(DateTime.UtcNow), stop);
            try { await Task.Delay(interval, stop); } catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One pass over all workspaces (also called directly by tests).</summary>
    public async Task RunOnceAsync(DateOnly today, CancellationToken ct)
    {
        // Billing first, so the rest of the pass sees who is suspended today. It belongs to no workspace.
        try
        {
            using var billing = scopes.CreateScope();
            await billing.ServiceProvider.GetRequiredService<ERP.Application.Platform.ISubscriptionLifecycle>().RunAsync(today, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "The daily billing run failed"); }
        try
        {
            using var platform = scopes.CreateScope();
            foreach (var job in platform.ServiceProvider.GetServices<ERP.Application.Platform.IPlatformJob>()) await job.RunAsync(today, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "A platform job failed"); }

        List<Guid> orgIds;
        using (var scope = scopes.CreateScope())
            orgIds = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Organizations.IgnoreQueryFilters().Select(o => o.Id).ToListAsync(ct);

        foreach (var orgId in orgIds)
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<IOrgContext>().Use(orgId);
            var modules = sp.GetRequiredService<IModuleAccess>();
            // A suspended or cancelled client gets no reminders, statements or other automatic work. Only housekeeping that must still happen runs.
            var blocked = (await sp.GetRequiredService<ERP.Application.Platform.ISubscriptionAccess>().GetAsync(orgId, ct)).Access == ERP.Domain.Platform.SubscriptionAccessLevel.Blocked;
            foreach (var job in sp.GetServices<IScheduledJob>())
            {
                try
                {
                    if (!job.RunsWhenOff && (blocked || (job.ModuleKey is { } key && !await modules.IsEnabledAsync(key, ct)))) continue;
                    await job.RunAsync(today, ct);
                }
                catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Scheduled job {Job} failed for workspace {Org}", job.Name, orgId); }
            }
        }
    }
}
