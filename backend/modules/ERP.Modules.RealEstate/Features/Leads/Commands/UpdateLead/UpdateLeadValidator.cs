using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateLeadValidator : AbstractValidator<UpdateLeadCommand> { public UpdateLeadValidator() => RuleFor(x => x.Input).SetValidator(new LeadInputValidator()); }
