using FluentValidation;

namespace ERP.Application.Workflow;

public sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachmentCommand>
{
    public UploadAttachmentValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
