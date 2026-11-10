using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class UpdateRegisterValidator : AbstractValidator<UpdateRegisterCommand>
{
    public UpdateRegisterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ReceiptHeader).MaximumLength(500);
        RuleFor(x => x.ReceiptFooter).MaximumLength(500);
    }
}
