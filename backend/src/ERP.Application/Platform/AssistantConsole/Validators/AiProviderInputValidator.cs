using FluentValidation;

namespace ERP.Application.Platform;

public sealed class AiProviderInputValidator : AbstractValidator<AiProviderInput>
{
    public AiProviderInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter a name for this service.").MaximumLength(80);
        RuleFor(x => x.BaseUrl).NotEmpty().WithMessage("Enter the service's address.").MaximumLength(300);
        RuleFor(x => x.BaseUrl).Must(u => AiEndpointRules.Problem(u) is null).WithMessage(x => AiEndpointRules.Problem(x.BaseUrl) ?? "").When(x => !string.IsNullOrWhiteSpace(x.BaseUrl));
        RuleFor(x => x.Model).NotEmpty().WithMessage("Enter the model name.").MaximumLength(150);
        RuleFor(x => x.ApiKey).Must(k => k!.Trim().Length is >= 8 and <= 1000 && !k.Trim().Any(char.IsWhiteSpace)).WithMessage("That does not look like an API key.")
            .When(x => !string.IsNullOrWhiteSpace(x.ApiKey));
    }
}
