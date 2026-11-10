using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Modules.Assistant.Domain;
using ERP.Modules.Assistant.Repositories;
using FluentValidation;

namespace ERP.Modules.Assistant.Features;

internal sealed class ConfirmAssistantActionHandler(
    IAssistantChatMessageRepository messages, IAssistantPendingActionRepository actions, AssistantCatalog catalog, ICurrentUser user, IClock clock)
    : ICommandHandler<ConfirmAssistantActionCommand, AssistantActionCard>
{
    private Guid Me => user.UserId ?? throw new ForbiddenException();

    /// <summary>Only ever reached by this person's click. It runs as them, on a fresh request that has passed all the usual checks, and the permission is checked again now.</summary>
    public async Task<AssistantActionCard> Handle(ConfirmAssistantActionCommand cmd, CancellationToken ct)
    {
        // 1. Only a pending suggestion that has not expired can be made.
        var row = await actions.GetOwnForUpdateAsync(cmd.Id, Me, ct);
        if (row.Status != AssistantPendingAction.Pending) return AssistantActionCard.From(row);
        var now = clock.UtcNow;
        if (now > row.ExpiresAt)
        {
            row.Status = AssistantPendingAction.Expired;
            row.Message = "This suggestion has expired. Ask again if you still want it.";
            await actions.SaveChangesAsync(ct);
            return AssistantActionCard.From(row);
        }

        // 2. Run the action as the caller; a rule it breaks becomes the card's message.
        var action = await catalog.FindActionAsync(row.ActionKey, ct) ?? throw new ForbiddenException();
        using var doc = JsonDocument.Parse(row.ArgumentsJson);
        AssistantActionResult? result = null;
        string? problem = null;
        try { result = await action.ExecuteAsync(doc.RootElement, ct); }
        catch (FieldValidationException e) { problem = e.Errors.Values.SelectMany(v => v).FirstOrDefault() ?? e.Message; }
        catch (ValidationException e) { problem = e.Errors.FirstOrDefault()?.ErrorMessage ?? e.Message; }
        catch (ConflictException e) { problem = e.Message; }
        catch (DomainException e) { problem = e.Message; }
        catch (NotFoundException e) { problem = e.Message; }

        // A command that failed may have left half-made changes in memory. They must not be saved with the outcome below.
        actions.DiscardChanges();

        // 3. Record the outcome on the card and in the chat.
        row = await actions.GetForUpdateAsync(cmd.Id, ct);
        row.Status = result is null ? AssistantPendingAction.Failed : AssistantPendingAction.Done;
        row.Message = result?.Message ?? problem;
        row.Link = result?.Link;
        messages.Add(new AssistantChatMessage { ConversationId = row.ConversationId, UserId = Me, At = now, FromUser = false, Text = result is null ? $"That change did not go through: {problem}" : $"Done: {result.Message}" });
        await actions.SaveChangesAsync(ct);
        return AssistantActionCard.From(row);
    }
}
