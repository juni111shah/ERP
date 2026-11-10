using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ReopenFiscalYearValidator : AbstractValidator<ReopenFiscalYearCommand>
{
    public ReopenFiscalYearValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100).WithMessage("Enter the fiscal year, for example 2026.");
        RuleFor(x => x.Reason).ValidVoidReason();
    }
}
