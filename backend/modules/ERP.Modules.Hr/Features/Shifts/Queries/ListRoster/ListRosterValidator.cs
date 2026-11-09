using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ListRosterValidator : AbstractValidator<ListRosterQuery> { public ListRosterValidator() { RuleFor(x => x.Year).InclusiveBetween(2000, 2100); RuleFor(x => x.Month).InclusiveBetween(1, 12); } }
