using FluentValidation;

namespace ERP.Modules.Manufacturing.Features;

public class CreateBomValidator : AbstractValidator<CreateBomCommand>
{
    public CreateBomValidator()
    {
        RuleForEach(x => x.Components).SetValidator(new BomComponentInputValidator());
        RuleFor(x => x.Components).Must(c => c is null || c.Select(x => x.ComponentItemId).Distinct().Count() == c.Count).WithMessage("A component can appear only once.");
        RuleFor(x => x).Must(x => x.Components is null || x.Components.All(c => c.ComponentItemId != x.ProducedItemId)).WithName("Components").WithMessage("A product cannot be a component of itself.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ProducedItemId).NotEmpty();
        RuleFor(x => x.OutputQuantity).GreaterThan(0);
    }
}
