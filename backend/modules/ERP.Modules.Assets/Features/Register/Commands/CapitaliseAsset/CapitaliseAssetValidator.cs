using ERP.Application.Finance;
using ERP.Modules.Assets.Domain;
using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class CapitaliseAssetValidator : AbstractValidator<CapitaliseAssetCommand>
{
    public CapitaliseAssetValidator()
    {
        RuleFor(x => x.Input.Source).IsInEnum();
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Reference).MaximumLength(100);
        RuleFor(x => x.Input.AccountId).NotNull().When(x => x.Input.Source is CapitalisationSource.Clearing or CapitalisationSource.Bank).WithMessage("Choose the account to credit.");
        RuleFor(x => x.Input.PartyId).NotNull().When(x => x.Input.Source == CapitalisationSource.Vendor).WithMessage("Choose the vendor.");
        RuleFor(x => x.Input.BillId).NotNull().When(x => x.Input.Source == CapitalisationSource.Bill).WithMessage("Choose the bill.");
    }
}
