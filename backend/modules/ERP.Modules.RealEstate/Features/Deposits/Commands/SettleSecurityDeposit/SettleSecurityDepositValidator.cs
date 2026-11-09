using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class SettleSecurityDepositValidator : AbstractValidator<SettleSecurityDepositCommand>
{
    public SettleSecurityDepositValidator()
    {
        RuleFor(x => x.Input.Date).NotEqual(default(DateOnly)).WithMessage("Enter the settlement date.");
        RuleFor(x => x.Input.Damages).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Input.Refund).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Input.DamagesDescription).MaximumLength(300);
        RuleFor(x => x.Input.RefundBankAccountId).NotEmpty().When(x => x.Input.Refund > 0).WithMessage("Choose the bank account the refund is paid from.");
        RuleForEach(x => x.Input.Arrears).ChildRules(a => a.RuleFor(y => y.Amount).GreaterThan(0).MustBeMoney());
    }
}
