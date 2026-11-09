using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class LoanInputValidator : AbstractValidator<LoanInput>
{
    public LoanInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Principal).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Instalments).InclusiveBetween(1, 120);
        RuleFor(x => x.StartYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.StartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.InstalmentAmount!.Value).GreaterThan(0).When(x => x.InstalmentAmount is not null).WithName("InstalmentAmount");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
