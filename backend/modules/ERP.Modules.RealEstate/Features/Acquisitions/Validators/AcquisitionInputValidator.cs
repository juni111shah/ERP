using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class AcquisitionInputValidator : AbstractValidator<AcquisitionInput>
{
    public AcquisitionInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.SellerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContractReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PurchasePrice).GreaterThan(0);
        RuleFor(x => x.DownPayment).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TransferFees).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherCosts).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.DownPayment <= x.PurchasePrice).WithName("downPayment").WithMessage("The down payment cannot exceed the purchase price.");
        RuleFor(x => x).Must(x => x.DownPayment + (x.Funding?.Sum(f => f.Principal) ?? 0) <= x.PurchasePrice).WithName("funding").WithMessage("The down payment and the financing together cannot exceed the purchase price.");
        RuleForEach(x => x.Funding).ChildRules(f =>
        {
            f.RuleFor(y => y.Type).IsInEnum();
            f.RuleFor(y => y.Lender).NotEmpty().MaximumLength(200);
            f.RuleFor(y => y.Principal).GreaterThan(0);
            f.RuleFor(y => y.AnnualRatePercent).InclusiveBetween(0, 100);
            f.RuleFor(y => y.TermMonths).InclusiveBetween(1, 600);
        });
    }
}
