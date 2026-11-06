using System.Runtime.CompilerServices;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Platform;
using FluentValidation;
using MediatR;

namespace ERP.Application.Assistant;

public sealed record AssistantTryMessage(ChatRole Role, string Text);
public sealed record AssistantTryInput(AssistantBot Bot, IReadOnlyList<AssistantTryMessage> Messages);

/// <summary>The founder tries the connected services from the console, whether or not the assistants are switched on yet.</summary>
public sealed record TryAssistant(AssistantTryInput Input) : IStreamRequest<AssistantEvent>;

public sealed class TryAssistantValidator : AbstractValidator<TryAssistant>
{
    public TryAssistantValidator()
    {
        RuleFor(x => x.Input.Messages).NotEmpty().WithMessage("Type a message.")
            .Must(m => m.Count <= 20).WithMessage("This conversation is too long. Clear it and start again.")
            .Must(m => m.Count == 0 || m[^1].Role == ChatRole.User).WithMessage("The last message must come from you.")
            .Must(m => m.All(x => x.Role is ChatRole.User or ChatRole.Assistant)).WithMessage("Only your messages and the assistant's replies can be sent.")
            .Must(m => m.All(x => !string.IsNullOrWhiteSpace(x.Text) && x.Text.Length <= 2000)).WithMessage("Each message must be 1 to 2,000 characters long.");
    }
}

internal sealed class TryAssistantHandler(AssistantRunner runner, IPlatformDirectory directory, ICurrentUser user, IValidator<TryAssistant> validator) : IStreamRequestHandler<TryAssistant, AssistantEvent>
{
    private const string TrySystemPrompt = "You are a helpful assistant being tried out by the owner of a business software product. Keep replies short. Reply in the language of the last message.";

    public async IAsyncEnumerable<AssistantEvent> Handle(TryAssistant request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (user.UserId is not { } id || !await directory.IsPlatformOwnerAsync(id, ct)) throw new ForbiddenException("Only the platform owner can try the AI assistant.");
        await validator.ValidateAndThrowAsync(request, ct);
        var history = request.Input.Messages.Select(m => new ChatMessage(m.Role, m.Text)).ToList();
        await foreach (var e in runner.RunAsync(new AssistantTurn(request.Input.Bot, TrySystemPrompt, history, []), ct)) yield return e;
    }
}
