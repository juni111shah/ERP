using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class PropertyInputValidator : AbstractValidator<PropertyInput>
{
    public PropertyInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PropertyType).IsInEnum();
        RuleFor(x => x.OwnershipType).IsInEnum();
        RuleFor(x => x.OwnerPartyId).NotNull().When(x => x.OwnershipType == OwnershipType.ManagedForOwner)
            .WithMessage("Choose the owner of a property managed for a third party.");
        RuleFor(x => x.CreditRentToOwner).Must(v => v != true).When(x => x.OwnershipType != OwnershipType.ManagedForOwner)
            .WithMessage("Only a property managed for an owner can credit its rent to the owner.");
        RuleFor(x => x.TotalUnits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IndicativeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ManagementFeePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.YearBuilt).InclusiveBetween(1800, 2200).When(x => x.YearBuilt is not null);
    }
}
