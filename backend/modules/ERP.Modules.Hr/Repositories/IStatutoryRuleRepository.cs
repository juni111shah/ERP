using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for statutory rules.</summary>
public interface IStatutoryRuleRepository : IRepository<StatutoryRule>
{
    /// <summary>The rule with its slabs, read-only. Throws NotFoundException.</summary>
    Task<StatutoryRule> GetWithSlabsAsync(Guid id, CancellationToken ct);

    /// <summary>The rule with its slabs, tracked for changes. Throws NotFoundException.</summary>
    Task<StatutoryRule> GetWithSlabsForUpdateAsync(Guid id, CancellationToken ct);
}
