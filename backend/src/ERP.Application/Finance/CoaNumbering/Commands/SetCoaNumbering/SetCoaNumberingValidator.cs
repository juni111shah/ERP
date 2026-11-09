using ERP.Domain.Finance;
using FluentValidation;

namespace ERP.Application.Finance;

public sealed class SetCoaNumberingValidator : AbstractValidator<SetCoaNumberingCommand>
{
    public SetCoaNumberingValidator() => RuleFor(x => x).Custom((x, ctx) =>
    {
        var error = new AccountCodeMask(x.Mode?.ToLowerInvariant() ?? "", x.Segments ?? [], x.Separator ?? "").Validate();
        if (error is not null) ctx.AddFailure("mask", error);
    });
}
