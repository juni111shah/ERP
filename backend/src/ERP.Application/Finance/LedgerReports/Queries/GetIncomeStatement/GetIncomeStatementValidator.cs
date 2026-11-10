using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetIncomeStatementValidator : AbstractValidator<GetIncomeStatementQuery>
{
    public GetIncomeStatementValidator() => ReportRange.Apply(this, x => (DateOnly?)x.From, x => x.To);
}
