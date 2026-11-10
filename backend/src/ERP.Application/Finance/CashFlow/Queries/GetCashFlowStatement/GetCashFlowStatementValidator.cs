using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetCashFlowStatementValidator : AbstractValidator<GetCashFlowStatementQuery>
{
    public GetCashFlowStatementValidator() => ReportRange.Apply(this, x => (DateOnly?)x.From, x => x.To);
}
