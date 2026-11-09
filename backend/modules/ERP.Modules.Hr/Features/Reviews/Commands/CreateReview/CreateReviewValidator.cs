using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewCommand> { public CreateReviewValidator() => RuleFor(x => x.Input).SetValidator(new ReviewInputValidator()); }
