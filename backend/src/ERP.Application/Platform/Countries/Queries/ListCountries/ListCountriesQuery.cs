using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The countries offered at onboarding, those with a country pack marked.</summary>
public sealed record ListCountriesQuery : IQuery<IReadOnlyList<CountryDto>>;
