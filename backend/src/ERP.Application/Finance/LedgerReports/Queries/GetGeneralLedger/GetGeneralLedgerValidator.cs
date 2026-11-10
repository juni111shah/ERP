using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetGeneralLedgerValidator : AbstractValidator<GetGeneralLedgerQuery>
{
    public GetGeneralLedgerValidator() { RuleFor(x => x.AccountId).NotEmpty(); ReportRange.Apply(this, x => x.From, x => x.To); }
}
