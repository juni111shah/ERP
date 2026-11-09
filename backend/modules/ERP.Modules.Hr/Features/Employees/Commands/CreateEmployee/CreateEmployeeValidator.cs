using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.Input).SetValidator(new EmployeeInputValidator());
        RuleFor(x => x.Input.BasicSalary).NotNull().WithMessage("The basic salary must be greater than zero.").OverridePropertyName("BasicSalary");
    }
}
