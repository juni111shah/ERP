using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateJobOpeningValidator : AbstractValidator<UpdateJobOpeningCommand> { public UpdateJobOpeningValidator() => RuleFor(x => x.Input).SetValidator(new JobOpeningInputValidator()); }
