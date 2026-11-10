using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateCandidateValidator : AbstractValidator<UpdateCandidateCommand> { public UpdateCandidateValidator() => RuleFor(x => x.Input).SetValidator(new CandidateInputValidator()); }
