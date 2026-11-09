using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateLeadValidator : AbstractValidator<CreateLeadCommand> { public CreateLeadValidator() => RuleFor(x => x.Input).SetValidator(new LeadInputValidator()); }
