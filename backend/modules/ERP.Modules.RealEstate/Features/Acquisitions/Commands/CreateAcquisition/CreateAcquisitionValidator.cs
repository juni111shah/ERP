using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateAcquisitionValidator : AbstractValidator<CreateAcquisitionCommand> { public CreateAcquisitionValidator() => RuleFor(x => x.Input).SetValidator(new AcquisitionInputValidator()); }
