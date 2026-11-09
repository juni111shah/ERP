using FluentValidation;

namespace ERP.Application.Parties;

public sealed class CreatePartyGroupValidator : AbstractValidator<CreatePartyGroupCommand> { public CreatePartyGroupValidator() => RuleFor(x => x.Input).SetValidator(new PartyGroupInputValidator()); }
