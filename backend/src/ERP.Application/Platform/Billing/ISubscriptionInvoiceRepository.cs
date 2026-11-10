using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>The amounts of an invoice, for the console's totals.</summary>
public sealed record SubscriptionInvoiceAmount(SubscriptionInvoiceStatus Status, decimal Total, DateOnly DueOn, DateOnly? PaidOn);

/// <summary>Data access for the platform's invoices to clients (not org-scoped: always filter on the client). Implemented in ERP.Infrastructure/Repositories/Platform/SubscriptionInvoiceRepository.cs.</summary>
public interface ISubscriptionInvoiceRepository : IRepository<SubscriptionInvoice>
{
    /// <summary>A client's invoices, newest first.</summary>
    Task<List<SubscriptionInvoice>> ListForClientAsync(Guid orgId, CancellationToken ct);

    /// <summary>The invoice when it belongs to <paramref name="orgId"/>, otherwise null.</summary>
    Task<SubscriptionInvoice?> FindForClientAsync(Guid id, Guid orgId, CancellationToken ct);

    /// <summary>The total of each client's open invoices.</summary>
    Task<Dictionary<Guid, decimal>> OpenBalanceByClientAsync(CancellationToken ct);

    /// <summary>The amounts of every invoice that is not void.</summary>
    Task<List<SubscriptionInvoiceAmount>> ListAmountsNotVoidAsync(CancellationToken ct);

    /// <summary>Whether the client has an open invoice due before <paramref name="today"/>, leaving out <paramref name="except"/>.</summary>
    Task<bool> HasOverdueAsync(Guid orgId, Guid? except, DateOnly today, CancellationToken ct);

    /// <summary>Invoices of every client (or one), newest first, optionally by state ("overdue" = open and past due, or a <see cref="SubscriptionInvoiceStatus"/> name).</summary>
    Task<List<SubscriptionInvoice>> ListForConsoleAsync(Guid? orgId, string? status, DateOnly today, CancellationToken ct);
}
