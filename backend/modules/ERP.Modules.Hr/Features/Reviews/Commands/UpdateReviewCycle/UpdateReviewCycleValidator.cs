using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateReviewCycleValidator : AbstractValidator<UpdateReviewCycleCommand> { public UpdateReviewCycleValidator() => RuleFor(x => x.Input).SetValidator(new ReviewCycleInputValidator()); }
