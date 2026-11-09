using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SaveRiskValidator : AbstractValidator<SaveRiskCommand>
{
    public SaveRiskValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Notes).MaximumLength(2000);
        RuleFor(x => x.Input.OwnerName).MaximumLength(150);
        RuleFor(x => x.Input.Kind).IsInEnum();
        RuleFor(x => x.Input.Severity).IsInEnum();
        RuleFor(x => x.Input.Status).IsInEnum();
    }
}
