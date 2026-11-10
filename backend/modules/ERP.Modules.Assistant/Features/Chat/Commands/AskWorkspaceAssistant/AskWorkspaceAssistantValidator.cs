using FluentValidation;

namespace ERP.Modules.Assistant.Features;

public sealed class AskWorkspaceAssistantValidator : AbstractValidator<AskWorkspaceAssistantCommand>
{
    public AskWorkspaceAssistantValidator()
    {
        RuleFor(x => x.Input.Text).NotEmpty().WithMessage("Type a question.").MaximumLength(WorkspaceAssistant.MaxMessageLength).WithMessage($"Please keep your question under {WorkspaceAssistant.MaxMessageLength:N0} characters.");
        RuleFor(x => x.Input.Page).MaximumLength(300);
    }
}
