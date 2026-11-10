using System.Runtime.CompilerServices;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Repositories;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Assistant.Features;

internal sealed class AskWorkspaceAssistantHandler(
    AssistantRunner runner, AssistantCatalog catalog, ISender sender, IAssistantConversationRepository conversations, IAssistantChatMessageRepository messages, IAssistantPendingActionRepository pending,
    IReadRepository<Organization> organizations, ISettingsService settingsService, ICurrentUser user, IClock clock, IValidator<AskWorkspaceAssistantCommand> validator) : IStreamRequestHandler<AskWorkspaceAssistantCommand, AssistantEvent>
{
    public async IAsyncEnumerable<AssistantEvent> Handle(AskWorkspaceAssistantCommand request, [EnumeratorCancellation] CancellationToken ct)
    {
        // 1. A valid question, an available assistant and messages left for today.
        var me = user.UserId ?? throw new ForbiddenException();
        await validator.ValidateAndThrowAsync(request, ct);
        var status = await sender.Send(new GetWorkspaceAssistantStatusQuery(), ct);
        if (!status.Available)
        {
            yield return new AssistantError("off", "The assistant is not available right now.");
            yield break;
        }

        var now = clock.UtcNow;
        var dayStart = now.Date;
        var dailyLimit = await settingsService.GetAsync<int?>(AssistantModule.DailyMessagesSetting, null, ct) ?? 30;
        if (await messages.CountAsync(m => m.UserId == me && m.FromUser && m.At >= dayStart, ct) >= dailyLimit)
        {
            yield return new AssistantError("user-limit", "You have used today's messages for the assistant. Please try again tomorrow.");
            yield break;
        }

        // 2. The chat (the caller's own, or a new one) with its recent history, and the question saved to it.
        var text = request.Input.Text.Trim();
        AssistantConversation? conversation = null;
        if (request.Input.ConversationId is { } id)
            conversation = await conversations.FindOwnForUpdateAsync(id, me, ct) ?? throw new NotFoundException();
        if (conversation is null)
        {
            conversation = new AssistantConversation { UserId = me, Title = WorkspaceAssistant.TitleFrom(text), StartedAt = now, LastMessageAt = now };
            conversations.Add(conversation);
        }
        var history = (await messages.ListLatestAsync(conversation.Id, WorkspaceAssistant.HistoryMessages, ct))
            .OrderBy(m => m.At).Select(m => new ChatMessage(m.FromUser ? ChatRole.User : ChatRole.Assistant, m.Text)).Append(new ChatMessage(ChatRole.User, text)).ToList();

        conversation.LastMessageAt = now;
        messages.Add(new AssistantChatMessage { ConversationId = conversation.Id, UserId = me, At = now, FromUser = true, Text = text });
        await messages.SaveChangesAsync(ct);
        yield return new AssistantData("conversation", new { id = conversation.Id });

        // 3. The rules, the context and the tools the caller may use.
        var org = await organizations.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);
        var system = WorkspaceAssistant.Rules + "\n\n" + WorkspaceAssistant.Context(DateOnly.FromDateTime(now), org.Name, org.BaseCurrency, request.Input.Page, await catalog.ReadersAsync(ct), await catalog.ActionsAsync(ct));
        var proposals = new ProposeActionTool(catalog, pending, user, clock, conversation.Id);
        IAssistantTool[] tools =
        [
            new SearchRecordsTool(sender), new ListRecordsTool(catalog), new RunReportTool(sender, user, clock), new GetOverviewTool(sender, user),
            new ListApprovalsTool(sender), new FindScreenTool(sender), proposals,
        ];

        // 4. Stream the reply; the words are saved at the end.
        var reply = new StringBuilder();
        var shown = 0;
        try
        {
            await foreach (var e in runner.RunAsync(new AssistantTurn(AssistantBot.Workspace, system, history, tools, WorkspaceAssistant.MaxReplyTokens), ct))
            {
                // Cards go out as soon as they exist, ahead of the words about them.
                for (; shown < proposals.Proposed.Count; shown++) yield return new AssistantData("action", AssistantActionCard.From(proposals.Proposed[shown]));
                if (e is AssistantServiceUsed) continue; // people in a workspace are not told which service answered
                if (e is AssistantDelta d) reply.Append(d.Text);
                yield return e;
            }
            for (; shown < proposals.Proposed.Count; shown++) yield return new AssistantData("action", AssistantActionCard.From(proposals.Proposed[shown]));
        }
        finally
        {
            // Saved even when the person leaves part way through a reply.
            if (reply.Length > 0)
            {
                messages.Add(new AssistantChatMessage { ConversationId = conversation.Id, UserId = me, At = clock.UtcNow, FromUser = false, Text = reply.ToString() });
                await messages.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
