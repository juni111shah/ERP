using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateAcquisitionValidator : AbstractValidator<UpdateAcquisitionCommand> { public UpdateAcquisitionValidator() => RuleFor(x => x.Input).SetValidator(new AcquisitionInputValidator()); }
