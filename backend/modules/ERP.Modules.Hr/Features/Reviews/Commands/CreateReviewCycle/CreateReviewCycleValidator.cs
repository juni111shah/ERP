using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateReviewCycleValidator : AbstractValidator<CreateReviewCycleCommand> { public CreateReviewCycleValidator() => RuleFor(x => x.Input).SetValidator(new ReviewCycleInputValidator()); }
