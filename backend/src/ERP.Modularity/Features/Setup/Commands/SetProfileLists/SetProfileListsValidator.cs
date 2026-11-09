using FluentValidation;

namespace ERP.Modularity.Features;

public sealed class SetProfileListsValidator : AbstractValidator<SetProfileListsCommand>
{
    public SetProfileListsValidator()
    {
        RuleForEach(x => x.Input.Units).NotEmpty().WithMessage("A unit cannot be empty.").MaximumLength(30);
        RuleForEach(x => x.Input.ItemCategories).NotEmpty().WithMessage("A category cannot be empty.").MaximumLength(100);
    }
}
