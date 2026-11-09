using FluentValidation;

namespace ERP.Application.Organizations;

public sealed class UpdateLegalEntityValidator : AbstractValidator<UpdateLegalEntityCommand>
{
    public UpdateLegalEntityValidator() => RuleFor(x => x.Input).SetValidator(new LegalEntityInputValidator());
}
