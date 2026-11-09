using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateTimesheetValidator : AbstractValidator<UpdateTimesheetCommand>
{
    public UpdateTimesheetValidator() => RuleFor(x => x.Input).SetValidator(new TimesheetInputValidator());
}
