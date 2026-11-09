using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for the connected AI services. Implemented in ERP.Infrastructure/Repositories/Platform/AiProviderRepository.cs.</summary>
public interface IAiProviderRepository : IRepository<AiProvider>
{
    /// <summary>Every service in the order they are tried.</summary>
    Task<List<AiProvider>> ListInOrderAsync(CancellationToken ct);

    /// <summary>The highest sort order in use, or null when there is no service.</summary>
    Task<int?> LastSortOrderAsync(CancellationToken ct);

    /// <summary>Every service, tracked for changes.</summary>
    Task<List<AiProvider>> ListForUpdateAsync(CancellationToken ct);
}
