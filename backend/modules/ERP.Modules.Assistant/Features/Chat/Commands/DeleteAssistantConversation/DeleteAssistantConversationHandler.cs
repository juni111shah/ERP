using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Assistant.Repositories;
using MediatR;

namespace ERP.Modules.Assistant.Features;

internal sealed class DeleteAssistantConversationHandler(
    IAssistantConversationRepository conversations, IAssistantChatMessageRepository messages, IAssistantPendingActionRepository actions, ICurrentUser user)
    : ICommandHandler<DeleteAssistantConversationCommand>
{
    private Guid Me => user.UserId ?? throw new ForbiddenException();

    public async Task<Unit> Handle(DeleteAssistantConversationCommand cmd, CancellationToken ct)
    {
        var c = await conversations.FindOwnForUpdateAsync(cmd.Id, Me, ct) ?? throw new NotFoundException();
        foreach (var m in await messages.ListForConversationForUpdateAsync(c.Id, ct)) messages.Remove(m);
        foreach (var a in await actions.ListForConversationForUpdateAsync(c.Id, ct)) actions.Remove(a);
        conversations.Remove(c);
        await conversations.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
