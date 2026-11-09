using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class UpdateBomValidator : AbstractValidator<UpdateBomCommand>
{
    public UpdateBomValidator()
    {
        RuleForEach(x => x.Components).SetValidator(new BomComponentInputValidator());
        RuleFor(x => x.Components).Must(c => c is null || c.Select(x => x.ComponentItemId).Distinct().Count() == c.Count).WithMessage("A component can appear only once.");
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OutputQuantity).GreaterThan(0);
    }
}
