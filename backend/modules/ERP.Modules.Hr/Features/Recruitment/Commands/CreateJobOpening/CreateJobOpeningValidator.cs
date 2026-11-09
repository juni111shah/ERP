using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateJobOpeningValidator : AbstractValidator<CreateJobOpeningCommand> { public CreateJobOpeningValidator() => RuleFor(x => x.Input).SetValidator(new JobOpeningInputValidator()); }
