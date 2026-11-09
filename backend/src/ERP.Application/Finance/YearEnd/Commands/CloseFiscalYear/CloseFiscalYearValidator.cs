using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CloseFiscalYearValidator : AbstractValidator<CloseFiscalYearCommand>
{
    public CloseFiscalYearValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100).WithMessage("Enter the fiscal year, for example 2026.");
    }
}
