using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class AddRosterValidator : AbstractValidator<AddRosterCommand>
{
    public AddRosterValidator()
    {
        RuleFor(x => x.Input.EmployeeIds).NotEmpty().Must(i => i.Count <= 500);
        RuleFor(x => x.Input.ToDate).GreaterThanOrEqualTo(x => x.Input.FromDate).WithMessage("The end date cannot be before the start date.");
    }
}
