using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateChecklistTemplateValidator : AbstractValidator<CreateChecklistTemplateCommand> { public CreateChecklistTemplateValidator() => RuleFor(x => x.Input).SetValidator(new ChecklistTemplateInputValidator()); }
