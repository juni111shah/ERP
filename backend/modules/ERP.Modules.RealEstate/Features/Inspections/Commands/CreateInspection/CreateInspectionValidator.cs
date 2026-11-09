using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateInspectionValidator : AbstractValidator<CreateInspectionCommand> { public CreateInspectionValidator() => RuleFor(x => x.Input).SetValidator(new InspectionInputValidator()); }
