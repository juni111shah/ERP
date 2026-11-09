using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateReceiptValidator : AbstractValidator<CreateReceiptCommand>
{
    public CreateReceiptValidator() => RuleFor(x => x.Input).SetValidator(new ReceiptInputValidator());
}
