namespace ERP.Application.Abstractions;

/// <summary>
/// Housekeeping that must run on its own (for example expiring leases past their end date). The host runs every job for every
/// workspace on a timer, inside that workspace's context, and only while the job's module is enabled. Jobs must be idempotent.
/// </summary>
public interface IScheduledJob
{
    string Name { get; }
    /// <summary>Module that must be enabled for the workspace; null = core.</summary>
    string? ModuleKey { get; }
    Task RunAsync(DateOnly today, CancellationToken ct);
}
