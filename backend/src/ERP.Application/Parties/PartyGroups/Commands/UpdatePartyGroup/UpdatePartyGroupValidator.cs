using FluentValidation;

namespace ERP.Application.Parties;

public sealed class UpdatePartyGroupValidator : AbstractValidator<UpdatePartyGroupCommand> { public UpdatePartyGroupValidator() => RuleFor(x => x.Input).SetValidator(new PartyGroupInputValidator()); }
