using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateBillValidator : AbstractValidator<UpdateBillCommand>
{
    public UpdateBillValidator() => RuleFor(x => x.Input).SetValidator(new BillInputValidator());
}
