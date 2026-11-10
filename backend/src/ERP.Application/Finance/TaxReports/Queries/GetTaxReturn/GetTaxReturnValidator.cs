using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetTaxReturnValidator : AbstractValidator<GetTaxReturnQuery>
{
    public GetTaxReturnValidator()
    {
        RuleFor(x => x.LegalEntityId).NotNull().WithMessage("Choose a legal entity: its country decides the return layout.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
    }
}
