using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ReverseOpeningBalanceValidator : AbstractValidator<ReverseOpeningBalanceCommand>
{
    public ReverseOpeningBalanceValidator()
    {
        RuleFor(x => x.JournalId).NotEmpty();
        RuleFor(x => x.Reason).ValidVoidReason();
        RuleFor(x => x.Date).MustBePlausibleDate();
    }
}
