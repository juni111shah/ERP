using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ChecklistTemplateInputValidator : AbstractValidator<ChecklistTemplateInput>
{
    public ChecklistTemplateInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum();
        RuleForEach(x => x.Items).ChildRules(i => { i.RuleFor(y => y.Title).NotEmpty().MaximumLength(200); i.RuleFor(y => y.DueOffsetDays).InclusiveBetween(-365, 365); });
    }
}
