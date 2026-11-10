using ERP.Modules.Hr.Domain;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class GeneratePayrollValidator : AbstractValidator<GeneratePayrollCommand>
{
    public GeneratePayrollValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Input.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Input.Notes).MaximumLength(500);
        RuleFor(x => x.Input.Notes).NotEmpty().When(x => x.Input.Kind == PayrollKind.OffCycle).WithMessage("An off-cycle run needs a note saying why.");
        RuleFor(x => x.Input.Kind).Must(k => k is PayrollKind.Regular or PayrollKind.OffCycle).WithMessage("Choose a regular or off-cycle run.");
    }
}
