using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class ReturnWorkOrderMaterialsValidator : AbstractValidator<ReturnWorkOrderMaterialsCommand>
{
    public ReturnWorkOrderMaterialsValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose at least one item to return.");
        RuleForEach(x => x.Lines).ChildRules(MaterialLineRules.Apply);
    }
}
