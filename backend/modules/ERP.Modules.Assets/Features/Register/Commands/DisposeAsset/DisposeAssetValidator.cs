using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class DisposeAssetValidator : AbstractValidator<DisposeAssetCommand>
{
    public DisposeAssetValidator()
    {
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Proceeds).GreaterThanOrEqualTo(0).MustBeMoney();
        RuleFor(x => x.Input.ProceedsAccountId).NotNull().When(x => x.Input.Proceeds > 0).WithMessage("Choose the account that received the proceeds.");
        RuleFor(x => x.Input.Reason).MaximumLength(400);
    }
}
