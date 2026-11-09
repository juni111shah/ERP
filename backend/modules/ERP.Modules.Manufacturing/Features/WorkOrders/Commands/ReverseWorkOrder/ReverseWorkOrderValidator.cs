using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class ReverseWorkOrderValidator : AbstractValidator<ReverseWorkOrderCommand>
{
    public ReverseWorkOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Say why this work order is being reversed.").MaximumLength(500);
    }
}
