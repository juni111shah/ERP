using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateChecklistTemplateValidator : AbstractValidator<UpdateChecklistTemplateCommand> { public UpdateChecklistTemplateValidator() => RuleFor(x => x.Input).SetValidator(new ChecklistTemplateInputValidator()); }
