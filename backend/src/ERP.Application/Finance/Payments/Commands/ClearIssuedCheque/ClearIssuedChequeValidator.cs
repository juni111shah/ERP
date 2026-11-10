using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ClearIssuedChequeValidator : AbstractValidator<ClearIssuedChequeCommand>
{
    public ClearIssuedChequeValidator() => RuleFor(x => x.Date).MustBePlausibleDate();
}
