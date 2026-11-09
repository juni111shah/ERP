using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for the rent instalments of leases. Implemented in RentInstalmentRepository.cs.</summary>
public interface IRentInstalmentRepository : IRepository<RentInstalment>
{
    /// <summary>The schedule of a lease, in sequence.</summary>
    Task<List<RentInstalment>> ListOfLeaseAsync(Guid leaseId, CancellationToken ct);

    /// <summary>The instalments of a lease that are not cancelled, tracked for changes.</summary>
    Task<List<RentInstalment>> ListNotCancelledOfLeaseForUpdateAsync(Guid leaseId, CancellationToken ct);

    /// <summary>The invoiced instalments of a lease without a credit note whose period runs past <paramref name="date"/>, tracked for changes.</summary>
    Task<List<RentInstalment>> ListInvoicedPastDateNotCreditedForUpdateAsync(Guid leaseId, DateOnly date, CancellationToken ct);

    /// <summary>Scheduled instalments due on or before <paramref name="asOf"/>, of one legal entity or (null) all, oldest first, tracked for changes.</summary>
    Task<List<RentInstalment>> ListScheduledDueForUpdateAsync(DateOnly asOf, Guid? legalEntityId, CancellationToken ct);

    /// <summary>The instalment when <paramref name="invoiceId"/> is its invoice, tracked for changes; null otherwise.</summary>
    Task<RentInstalment?> FindInvoicedByForUpdateAsync(Guid id, Guid invoiceId, CancellationToken ct);

    /// <summary>The instalments invoiced by the given invoices.</summary>
    Task<List<RentInstalment>> ListOfInvoicesAsync(List<Guid> invoiceIds, CancellationToken ct);

    /// <summary>The instalments of the leases that are not cancelled, earliest due first.</summary>
    Task<List<RentInstalment>> ListNotCancelledOfLeasesByDueDateAsync(List<Guid> leaseIds, CancellationToken ct);

    /// <summary>The lease of each of the instalments, by id.</summary>
    Task<Dictionary<Guid, Guid>> MapLeaseIdsAsync(List<Guid> ids, CancellationToken ct);
}
