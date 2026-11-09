using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for the messages of public assistant conversations. Implemented in ERP.Infrastructure/Repositories/Platform/BrandMessageRepository.cs.</summary>
public interface IBrandMessageRepository : IRepository<BrandMessage>
{
    /// <summary>The newest <paramref name="take"/> messages of a conversation, newest first.</summary>
    Task<List<BrandMessage>> ListLatestAsync(Guid conversationId, int take, CancellationToken ct);

    /// <summary>The visitor's first message of each conversation in <paramref name="conversationIds"/> that has one.</summary>
    Task<Dictionary<Guid, string>> FirstVisitorMessagesAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken ct);

    /// <summary>A conversation's messages, oldest first.</summary>
    Task<List<BrandMessageRow>> ListRowsAsync(Guid conversationId, CancellationToken ct);
}
