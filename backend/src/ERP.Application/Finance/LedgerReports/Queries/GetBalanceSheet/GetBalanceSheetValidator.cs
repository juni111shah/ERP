using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetBalanceSheetValidator : AbstractValidator<GetBalanceSheetQuery>
{
    public GetBalanceSheetValidator() => RuleFor(x => x.AsOf).NotEqual(default(DateOnly)).WithMessage("Enter the report date.");
}
