using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateStatutoryRuleValidator : AbstractValidator<UpdateStatutoryRuleCommand> { public UpdateStatutoryRuleValidator() => RuleFor(x => x.Input).SetValidator(new StatutoryRuleInputValidator()); }
