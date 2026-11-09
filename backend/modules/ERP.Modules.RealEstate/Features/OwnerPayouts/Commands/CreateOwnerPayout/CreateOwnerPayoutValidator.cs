using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateOwnerPayoutValidator : AbstractValidator<CreateOwnerPayoutCommand>
{
    public CreateOwnerPayoutValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.OwnerPartyId).NotEmpty();
        RuleFor(x => x.Input.To).GreaterThanOrEqualTo(x => x.Input.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Method).IsInEnum();
        RuleFor(x => x.Input.BankAccountId).NotEmpty().When(x => x.Input.Method == OwnerPayoutMethod.Payment).WithMessage("Choose the bank account the owner is paid from.");
        RuleFor(x => x.Input.Notes).MaximumLength(500);
    }
}
