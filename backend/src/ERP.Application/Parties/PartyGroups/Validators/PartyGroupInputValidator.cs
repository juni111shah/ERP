using FluentValidation;

namespace ERP.Application.Parties;

public sealed class PartyGroupInputValidator : AbstractValidator<PartyGroupInput>
{
    public PartyGroupInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
