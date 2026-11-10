using FluentValidation;

namespace ERP.Application.Platform;

public sealed class UpdateAiProviderValidator : AbstractValidator<UpdateAiProviderCommand>
{
    public UpdateAiProviderValidator() => RuleFor(x => x.Input).SetValidator(new AiProviderInputValidator());
}
