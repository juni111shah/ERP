using ERP.Application.Abstractions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Repositories;

/// <summary>Data access for retainer contracts. Implemented in RetainerContractRepository.cs.</summary>
public interface IRetainerContractRepository : IRepository<RetainerContract>
{
    /// <summary>The retainer, tracked for changes; null when it does not exist.</summary>
    Task<RetainerContract?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The legal entity's active retainers.</summary>
    Task<List<RetainerContract>> ListActiveAsync(Guid legalEntityId, CancellationToken ct);
}
