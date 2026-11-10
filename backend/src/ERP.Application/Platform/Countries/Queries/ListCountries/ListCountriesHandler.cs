using ERP.Application.Common;
using ERP.Application.Templates;

namespace ERP.Application.Platform;

internal sealed class ListCountriesHandler(CountryPackRegistry packs) : IQueryHandler<ListCountriesQuery, IReadOnlyList<CountryDto>>
{
    /// <summary>Installed packs win over the generic data (their defaults come from the pack); the rest keep the neutral entry.</summary>
    public Task<IReadOnlyList<CountryDto>> Handle(ListCountriesQuery query, CancellationToken ct)
    {
        var result = CountryCatalog.Generic
            .Where(c => packs.Find(c.Code) is null)
            .Select(c => new CountryDto(c.Code, c.Name, c.Currency, c.TimeZone, false))
            .Concat(packs.All.Select(p => new CountryDto(p.CountryCode, p.Name, p.DefaultCurrency, p.DefaultTimeZone, true)))
            .OrderBy(c => c.Name).ToList();
        return Task.FromResult<IReadOnlyList<CountryDto>>(result);
    }
}
