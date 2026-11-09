using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for public assistant conversations. Implemented in ERP.Infrastructure/Repositories/Platform/BrandConversationRepository.cs.</summary>
public interface IBrandConversationRepository : IRepository<BrandConversation>
{
    /// <summary>The conversation, tracked for changes, or null.</summary>
    Task<BrandConversation?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>One page of conversations, those needing review first, then the latest, with the total count.</summary>
    Task<(List<BrandConversation> Rows, int Total)> ListPageAsync(int page, int size, CancellationToken ct);
}
