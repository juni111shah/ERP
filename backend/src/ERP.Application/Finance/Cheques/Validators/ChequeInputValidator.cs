using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Rules for a cheque body; shared by register and replace cheque.</summary>
public sealed class ChequeInputValidator : AbstractValidator<ChequeInput>
{
    public ChequeInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.InstrumentNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BankName).MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
