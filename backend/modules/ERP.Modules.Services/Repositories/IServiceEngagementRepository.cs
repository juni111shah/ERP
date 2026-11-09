using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Repositories;

/// <summary>Data access for service engagements. Implemented in ServiceEngagementRepository.cs.</summary>
public interface IServiceEngagementRepository : IRepository<ServiceEngagement>
{
    /// <summary>The engagement, tracked for changes; null when it does not exist.</summary>
    Task<ServiceEngagement?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The engagement, tracked for changes. Throws InvalidOperationException when it does not exist.</summary>
    Task<ServiceEngagement> FirstForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The number of active engagements of the legal entity.</summary>
    Task<int> CountActiveAsync(Guid legalEntityId, CancellationToken ct);

    /// <summary>The contract value of the legal entity's active engagements together.</summary>
    Task<decimal> TotalActiveContractValueAsync(Guid legalEntityId, CancellationToken ct);
}
