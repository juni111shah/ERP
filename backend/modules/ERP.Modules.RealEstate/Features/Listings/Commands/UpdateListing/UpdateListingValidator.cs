using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateListingValidator : AbstractValidator<UpdateListingCommand> { public UpdateListingValidator() => RuleFor(x => x.Input).SetValidator(new ListingInputValidator()); }
