using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class CreateRegisterValidator : AbstractValidator<CreateRegisterCommand>
{
    public CreateRegisterValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ReceiptHeader).MaximumLength(500);
        RuleFor(x => x.ReceiptFooter).MaximumLength(500);
    }
}
