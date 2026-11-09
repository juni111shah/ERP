using ERP.Domain.Parties;
using FluentValidation;

namespace ERP.Application.Parties;

public sealed class CreatePartyValidator : AbstractValidator<CreatePartyCommand>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Input.Email));
        RuleFor(x => x.Input.Roles).NotEqual(PartyRoles.None).WithMessage("Choose at least one role (customer, vendor...).");
        RuleFor(x => x.Input.CurrencyCode).Length(3).When(x => x.Input.CurrencyCode is not null);
        RuleFor(x => x.Input.CreditLimit).Must(v => v is null || (v >= 0 && decimal.Round(v.Value, 2) == v)).WithMessage("The credit limit cannot be negative and can have at most 2 decimal places.");
        RuleFor(x => x.Input.Phone).MaximumLength(50);
        RuleFor(x => x.Input.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Input.IdNumber).MaximumLength(50);
        RuleFor(x => x.Input.Iban).MaximumLength(50);
        RuleFor(x => x.Input.Address).MaximumLength(1000);
    }
}
