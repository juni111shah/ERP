using ERP.Application.Common;
using ERP.Domain.Finance;
using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Rules for a payment body.</summary>
public sealed class PaymentInputValidator : AbstractValidator<PaymentInput>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Date).MustBePlausibleDate();
        RuleFor(x => x.ChequeDate).MustBePlausibleDate();
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.ChequeNo).NotEmpty().MaximumLength(50).When(x => x.Method == SettlementMethod.Instrument).WithMessage("Enter the cheque number.");
    }
}
