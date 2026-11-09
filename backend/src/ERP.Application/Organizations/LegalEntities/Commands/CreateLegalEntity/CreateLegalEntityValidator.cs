using FluentValidation;

namespace ERP.Application.Organizations;

public sealed class CreateLegalEntityValidator : AbstractValidator<CreateLegalEntityCommand>
{
    public CreateLegalEntityValidator() => RuleFor(x => x.Input).SetValidator(new LegalEntityInputValidator());
}
