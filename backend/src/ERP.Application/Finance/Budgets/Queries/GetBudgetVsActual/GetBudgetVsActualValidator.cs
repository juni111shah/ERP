using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetBudgetVsActualValidator : AbstractValidator<GetBudgetVsActualQuery>
{
    public GetBudgetVsActualValidator() { RuleFor(x => x.FromMonth).InclusiveBetween(1, 12); RuleFor(x => x.ToMonth).InclusiveBetween(1, 12).GreaterThanOrEqualTo(x => x.FromMonth); }
}
