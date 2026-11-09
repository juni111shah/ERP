using FluentValidation;

namespace ERP.Application.Platform;

public sealed class CreateAiProviderValidator : AbstractValidator<CreateAiProviderCommand>
{
    public CreateAiProviderValidator()
    {
        RuleFor(x => x.Input).SetValidator(new AiProviderInputValidator());
        RuleFor(x => x.Input.ApiKey).NotEmpty().WithMessage("Paste the API key.");
    }
}
