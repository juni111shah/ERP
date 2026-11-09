using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateRenewalDraftsValidator : AbstractValidator<CreateRenewalDraftsCommand>
{
    public CreateRenewalDraftsValidator()
    {
        RuleFor(x => x.LeaseIds).NotEmpty().Must(l => l.Count <= 200).WithMessage("Select at most 200 leases.");
        RuleFor(x => x.UpliftPercent).InclusiveBetween(-50, 100);
    }
}
