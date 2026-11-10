using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SaveAllocationValidator : AbstractValidator<SaveAllocationCommand>
{
    public SaveAllocationValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.UserId).NotEmpty();
        RuleFor(x => x.Input.HoursPerWeek).GreaterThan(0).LessThanOrEqualTo(168);
        RuleFor(x => x.Input).Must(i => i.ToDate >= i.FromDate).WithName("toDate").WithMessage("The end date cannot be before the start date.");
    }
}
