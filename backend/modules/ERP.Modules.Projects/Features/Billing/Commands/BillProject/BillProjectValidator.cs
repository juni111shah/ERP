using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class BillProjectValidator : AbstractValidator<BillProjectCommand>
{
    public BillProjectValidator() => RuleFor(x => x.Input).SetValidator(new BillProjectInputValidator());
}
