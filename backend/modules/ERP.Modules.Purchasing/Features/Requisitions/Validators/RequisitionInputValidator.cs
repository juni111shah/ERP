using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class RequisitionInputValidator : AbstractValidator<RequisitionInput>
{
    public RequisitionInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RequesterName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Requisition must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.EstimatedUnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
