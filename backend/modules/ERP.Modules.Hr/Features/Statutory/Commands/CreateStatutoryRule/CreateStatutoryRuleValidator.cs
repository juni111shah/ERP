using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateStatutoryRuleValidator : AbstractValidator<CreateStatutoryRuleCommand> { public CreateStatutoryRuleValidator() => RuleFor(x => x.Input).SetValidator(new StatutoryRuleInputValidator()); }
