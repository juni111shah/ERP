using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateTimesheetValidator : AbstractValidator<CreateTimesheetCommand>
{
    public CreateTimesheetValidator() => RuleFor(x => x.Input).SetValidator(new TimesheetInputValidator());
}
