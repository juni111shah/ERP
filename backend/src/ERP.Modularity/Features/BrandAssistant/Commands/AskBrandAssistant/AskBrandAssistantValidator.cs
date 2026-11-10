using FluentValidation;

namespace ERP.Modularity.Features;

public sealed class AskBrandAssistantValidator : AbstractValidator<AskBrandAssistantCommand>
{
    public AskBrandAssistantValidator()
    {
        RuleFor(x => x.Input.Text).NotEmpty().WithMessage("Type a question.").MaximumLength(BrandAssistant.MaxMessageLength).WithMessage($"Please keep your question under {BrandAssistant.MaxMessageLength:N0} characters.");
    }
}
