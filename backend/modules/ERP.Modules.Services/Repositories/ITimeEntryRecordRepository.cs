using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Repositories;

/// <summary>Data access for time entries. Implemented in TimeEntryRecordRepository.cs.</summary>
public interface ITimeEntryRecordRepository : IRepository<TimeEntryRecord>
{
    /// <summary>The time entry, tracked for changes; null when it does not exist.</summary>
    Task<TimeEntryRecord?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The engagement's approved, billable, not yet invoiced time dated from <paramref name="from"/> to <paramref name="to"/>, tracked for changes.</summary>
    Task<List<TimeEntryRecord>> ListUninvoicedApprovedForUpdateAsync(Guid engagementId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>The engagement's approved, billable, not yet invoiced time, limited to the ids and the dates when given, tracked for changes.</summary>
    Task<List<TimeEntryRecord>> ListReadyToInvoiceForUpdateAsync(Guid engagementId, IReadOnlyList<Guid>? ids, DateOnly? from, DateOnly? to, CancellationToken ct);

    /// <summary>The engagement's billable hours dated from <paramref name="start"/> to <paramref name="end"/> that nobody has approved yet.</summary>
    Task<decimal> UnapprovedBillableHoursAsync(Guid engagementId, DateOnly start, DateOnly end, CancellationToken ct);

    /// <summary>The time billed on the invoice, tracked for changes.</summary>
    Task<List<TimeEntryRecord>> ListOnInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct);

    /// <summary>The legal entity's logged hours, every entry or only the billable ones.</summary>
    Task<decimal> TotalHoursAsync(Guid legalEntityId, bool billableOnly, CancellationToken ct);
}
