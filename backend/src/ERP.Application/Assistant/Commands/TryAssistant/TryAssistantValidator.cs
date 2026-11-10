using FluentValidation;

namespace ERP.Application.Assistant;

public sealed class TryAssistantValidator : AbstractValidator<TryAssistantCommand>
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
