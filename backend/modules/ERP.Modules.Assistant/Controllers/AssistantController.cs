using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Assistant.Features;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Assistant.Controllers;

/// <summary>The workspace AI assistant: availability, chats, messages and the changes it suggests.</summary>
[ModuleGate(AssistantModule.Key)]
[Route("api/v1/m/assistant")]
[RequirePermission(AssistantModule.ChatsUse)]
public sealed class AssistantController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("status")]
    public Task<WorkspaceAssistantStatus> Status(CancellationToken ct)
        => Mediator.Send(new GetWorkspaceAssistantStatusQuery(), ct);

    [HttpGet("conversations")]
    public Task<IReadOnlyList<AssistantConversationRow>> Conversations(CancellationToken ct)
        => Mediator.Send(new ListAssistantConversationsQuery(), ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<AssistantConversationDetail> Conversation(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAssistantConversationQuery(id), ct);

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAssistantConversationCommand(id), ct);
        return NoContent();
    }

    // Asking changes nothing in the business, so a view-only workspace can still ask. Confirming a change stays blocked there.
    [HttpPost("messages")]
    [AllowedInReadOnlyWorkspace]
    public IResult Ask(WorkspaceMessageInput input, CancellationToken ct)
        => new AssistantStreamResult(Mediator.CreateStream(new AskWorkspaceAssistantCommand(input), ct));

    [HttpPost("actions/{id:guid}/confirm")]
    public Task<AssistantActionCard> Confirm(Guid id, CancellationToken ct)
        => Mediator.Send(new ConfirmAssistantActionCommand(id), ct);

    [HttpPost("actions/{id:guid}/cancel")]
    [AllowedInReadOnlyWorkspace]
    public Task<AssistantActionCard> Cancel(Guid id, CancellationToken ct)
        => Mediator.Send(new CancelAssistantActionCommand(id), ct);
}
