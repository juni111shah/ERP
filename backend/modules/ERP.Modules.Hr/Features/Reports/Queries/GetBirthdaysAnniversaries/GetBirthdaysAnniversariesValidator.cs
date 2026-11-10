using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class GetBirthdaysAnniversariesValidator : AbstractValidator<GetBirthdaysAnniversariesQuery>
{
    public GetBirthdaysAnniversariesValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 366);
}
