using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListBrandConversationsHandler(IBrandConversationRepository conversations, IBrandMessageRepository messages)
    : IQueryHandler<ListBrandConversationsQuery, PagedResult<BrandConversationRow>>
{
    public async Task<PagedResult<BrandConversationRow>> Handle(ListBrandConversationsQuery query, CancellationToken ct)
    {
        var (page, total) = await conversations.ListPageAsync(query.PageNumber, query.Size, ct);
        var ids = page.Select(c => c.Id).ToList();
        var first = await messages.FirstVisitorMessagesAsync(ids, ct);
        return new(page.Select(c => new BrandConversationRow(c.Id, c.StartedAt, c.LastMessageAt, c.VisitorMessages, c.NeedsReview, first.GetValueOrDefault(c.Id))).ToList(), total, query.PageNumber, query.Size);
    }
}
