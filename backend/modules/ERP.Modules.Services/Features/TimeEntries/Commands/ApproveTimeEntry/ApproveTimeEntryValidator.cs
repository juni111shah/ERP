using FluentValidation;

namespace ERP.Modules.Services.Features;

public class ApproveTimeEntryValidator : AbstractValidator<ApproveTimeEntryCommand>
{
    public ApproveTimeEntryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
