using FluentValidation;

namespace ERP.Application.Workflow;

public sealed class ListAttachmentsValidator : AbstractValidator<ListAttachmentsQuery>
{
    public ListAttachmentsValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().WithMessage("Owner type and id are required.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner type and id are required.");
    }
}
