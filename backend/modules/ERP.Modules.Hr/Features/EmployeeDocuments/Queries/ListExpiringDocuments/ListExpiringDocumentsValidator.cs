using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ListExpiringDocumentsValidator : AbstractValidator<ListExpiringDocumentsQuery>
{
    public ListExpiringDocumentsValidator() => RuleFor(x => x.Days).InclusiveBetween(0, 3650);
}
