using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

public sealed class TransferFundsValidator : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the transfer date.");
        RuleFor(x => x.Memo).MaximumLength(300);
        RuleFor(x => x).Must(x => x.FromAccountId != x.ToAccountId).WithName("toAccountId").WithMessage("Choose two different accounts.");
    }
}
