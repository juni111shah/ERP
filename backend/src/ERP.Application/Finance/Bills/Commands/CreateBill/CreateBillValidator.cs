using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateBillValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillValidator() => RuleFor(x => x.Input).SetValidator(new BillInputValidator());
}
