using System.Runtime.CompilerServices;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Platform;
using FluentValidation;
using MediatR;

namespace ERP.Application.Assistant;

internal sealed class TryAssistantHandler(AssistantRunner runner, IPlatformDirectory directory, ICurrentUser user, IValidator<TryAssistantCommand> validator) : IStreamRequestHandler<TryAssistantCommand, AssistantEvent>
{
    private const string TrySystemPrompt = "You are a helpful assistant being tried out by the owner of a business software product. Keep replies short. Reply in the language of the last message.";

    public async IAsyncEnumerable<AssistantEvent> Handle(TryAssistantCommand request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (user.UserId is not { } id || !await directory.IsPlatformOwnerAsync(id, ct)) throw new ForbiddenException("Only the platform owner can try the AI assistant.");
        await validator.ValidateAndThrowAsync(request, ct);
        var history = request.Input.Messages.Select(m => new ChatMessage(m.Role, m.Text)).ToList();
        await foreach (var e in runner.RunAsync(new AssistantTurn(request.Input.Bot, TrySystemPrompt, history, []), ct)) yield return e;
    }
}
