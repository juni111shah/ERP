using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Repositories;

/// <summary>Data access for the invoiced periods of retainers. Implemented in RetainerBillingPeriodRepository.cs.</summary>
public interface IRetainerBillingPeriodRepository : IRepository<RetainerBillingPeriod>
{
    /// <summary>The retainer's invoiced period starting on <paramref name="periodStart"/>; null when it was not invoiced.</summary>
    Task<RetainerBillingPeriod?> FindForPeriodAsync(Guid retainerId, DateOnly periodStart, CancellationToken ct);

    /// <summary>Start and end of every invoiced period of the retainer.</summary>
    Task<List<BilledPeriod>> ListBilledPeriodsAsync(Guid retainerId, CancellationToken ct);

    /// <summary>The periods billed by the invoice, tracked for changes.</summary>
    Task<List<RetainerBillingPeriod>> ListOnInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct);
}

/// <summary>The dates of one invoiced retainer period.</summary>
public sealed record BilledPeriod(DateOnly PeriodStart, DateOnly PeriodEnd);
