using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateListingValidator : AbstractValidator<CreateListingCommand> { public CreateListingValidator() => RuleFor(x => x.Input).SetValidator(new ListingInputValidator()); }
