using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class IssueWorkOrderMaterialsValidator : AbstractValidator<IssueWorkOrderMaterialsCommand>
{
    public IssueWorkOrderMaterialsValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleForEach(x => x.Lines).ChildRules(MaterialLineRules.Apply);
    }
}
