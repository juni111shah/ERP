using FluentValidation;

namespace ERP.Modules.Services.Features;

public class InvoiceMilestoneValidator : AbstractValidator<InvoiceMilestoneCommand>
{
    public InvoiceMilestoneValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
