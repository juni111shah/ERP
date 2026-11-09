using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator()
    {
        RuleFor(x => x.Input.OwnerType).Must(t => t is "project" or "task").WithMessage("Comments go on a project or a task.");
        RuleFor(x => x.Input.OwnerId).NotEmpty();
        RuleFor(x => x.Input.Text).NotEmpty().MaximumLength(2000);
    }
}
