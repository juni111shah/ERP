using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class GetOwnerStatementValidator : AbstractValidator<GetOwnerStatementQuery>
{
    public GetOwnerStatementValidator() { RuleFor(x => x.OwnerPartyId).NotEmpty(); RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From); }
}
