using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class PreviewProjectBillValidator : AbstractValidator<PreviewProjectBillQuery>
{
    public PreviewProjectBillValidator() => RuleFor(x => x.Input).SetValidator(new BillProjectInputValidator());
}
