using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class DisburseLoanValidator : AbstractValidator<DisburseLoanCommand>
{
    public DisburseLoanValidator() { RuleFor(x => x.BankAccountId).NotEmpty().WithMessage("Choose the bank account."); RuleFor(x => x.Date).MustBePlausibleDate(); }
}
