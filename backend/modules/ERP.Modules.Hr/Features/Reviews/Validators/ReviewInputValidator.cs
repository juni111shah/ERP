using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ReviewInputValidator : AbstractValidator<ReviewInput>
{
    public ReviewInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.ReviewerName).MaximumLength(150);
        RuleFor(x => x.OverallRating).InclusiveBetween(1, 5).When(x => x.OverallRating is not null);
        RuleFor(x => x.Goals).MaximumLength(4000);
        RuleFor(x => x.Comments).MaximumLength(4000);
        RuleFor(x => x.Competencies).NotNull().Must(c => c is null || c.Count <= 30).WithMessage("At most 30 competencies.");
        RuleForEach(x => x.Competencies).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            c.RuleFor(x => x.Score).InclusiveBetween(1, 5);
            c.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}
