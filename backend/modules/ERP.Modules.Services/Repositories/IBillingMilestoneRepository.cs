using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Repositories;

/// <summary>Data access for billing milestones. Implemented in BillingMilestoneRepository.cs.</summary>
public interface IBillingMilestoneRepository : IRepository<BillingMilestone>
{
    /// <summary>The milestone, tracked for changes; null when it does not exist.</summary>
    Task<BillingMilestone?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The amounts of the engagement's milestones together.</summary>
    Task<decimal> TotalForEngagementAsync(Guid engagementId, CancellationToken ct);

    /// <summary>The milestones billed by the invoice (or, when given, the milestone the invoice was raised from), tracked for changes.</summary>
    Task<List<BillingMilestone>> ListForInvoiceForUpdateAsync(Guid invoiceId, Guid? sourceId, CancellationToken ct);

    /// <summary>The amounts of the legal entity's milestones in a status together.</summary>
    Task<decimal> TotalInStatusAsync(Guid legalEntityId, MilestoneStatus status, CancellationToken ct);
}
