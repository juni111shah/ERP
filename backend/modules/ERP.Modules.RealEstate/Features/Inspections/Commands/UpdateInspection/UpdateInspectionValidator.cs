using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateInspectionValidator : AbstractValidator<UpdateInspectionCommand> { public UpdateInspectionValidator() => RuleFor(x => x.Input).SetValidator(new InspectionInputValidator()); }
