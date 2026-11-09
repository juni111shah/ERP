using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class ChangeListingStatusValidator : AbstractValidator<ChangeListingStatusCommand> { public ChangeListingStatusValidator() => RuleFor(x => x.Status).IsInEnum(); }
