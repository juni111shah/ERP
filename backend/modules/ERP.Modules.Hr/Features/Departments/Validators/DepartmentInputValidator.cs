using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class DepartmentInputValidator : AbstractValidator<DepartmentInput>
{
    public DepartmentInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}
