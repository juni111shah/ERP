using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Assistant.Repositories;

namespace ERP.Modules.Assistant.Features;

internal sealed class GetAssistantConversationHandler(
    IAssistantConversationRepository conversations, IAssistantChatMessageRepository messages, IAssistantPendingActionRepository actions, ICurrentUser user)
    : IQueryHandler<GetAssistantConversationQuery, AssistantConversationDetail>
{
    private Guid Me => user.UserId ?? throw new ForbiddenException();

    public async Task<AssistantConversationDetail> Handle(GetAssistantConversationQuery q, CancellationToken ct)
    {
        var c = await conversations.FindOwnAsync(q.Id, Me, ct) ?? throw new NotFoundException();
        var list = await messages.ListRowsAsync(c.Id, ct);
        var cards = await actions.ListForConversationAsync(c.Id, ct);
        return new(c.Id, c.Title, list, cards.Select(AssistantActionCard.From).ToList());
    }
}
