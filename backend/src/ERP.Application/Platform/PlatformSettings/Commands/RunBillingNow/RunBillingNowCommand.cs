using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Runs the daily subscription pass now: trials ending, renewals, overdue invoices and suspensions.</summary>
public sealed record RunBillingNowCommand : ICommand<LifecycleResult>;
