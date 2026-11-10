using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class RecordSecurityDepositValidator : AbstractValidator<RecordSecurityDepositCommand>
{
    public RecordSecurityDepositValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the date.");
    }
}
