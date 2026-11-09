using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Assistant.Repositories;

namespace ERP.Modules.Assistant.Features;

internal sealed class ListAssistantConversationsHandler(IAssistantConversationRepository conversations, ICurrentUser user)
    : IQueryHandler<ListAssistantConversationsQuery, IReadOnlyList<AssistantConversationRow>>
{
    private Guid Me => user.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<AssistantConversationRow>> Handle(ListAssistantConversationsQuery q, CancellationToken ct)
        => await conversations.ListRecentRowsAsync(Me, ct);
}
