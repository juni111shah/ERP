using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class UpdateReviewValidator : AbstractValidator<UpdateReviewCommand> { public UpdateReviewValidator() => RuleFor(x => x.Input).SetValidator(new ReviewInputValidator()); }
