using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateCandidateValidator : AbstractValidator<CreateCandidateCommand> { public CreateCandidateValidator() => RuleFor(x => x.Input).SetValidator(new CandidateInputValidator()); }
