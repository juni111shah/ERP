using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetTrialBalanceValidator : AbstractValidator<GetTrialBalanceQuery>
{
    public GetTrialBalanceValidator() => ReportRange.Apply(this, x => x.From, x => x.To);
}
