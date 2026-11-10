using ERP.Application.Common;
using ERP.Domain.Finance;
using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Rules for a receipt body.</summary>
public sealed class ReceiptInputValidator : AbstractValidator<ReceiptInput>
{
    public ReceiptInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Date).MustBePlausibleDate();
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
        RuleFor(x => x.Method).IsInEnum().Must(m => m != SettlementMethod.Instrument).WithMessage("Cheques are recorded in the cheque register.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}
