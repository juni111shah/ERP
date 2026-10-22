using ERP.Application.Common;
using ERP.Application.Templates;
using MediatR;

namespace ERP.Application.Platform;

/// <summary>A country offered at onboarding. <c>HasPack</c> = a country pack supplies tax codes and country-specific fields.</summary>
public sealed record CountryDto(string Code, string Name, string DefaultCurrency, string DefaultTimeZone, bool HasPack);
public sealed record CurrencyOptionDto(string Code, string Name, string Symbol, int Decimals);

/// <summary>Neutral ISO data for countries without a pack (name, usual currency, time zone). Country rules never live here.</summary>
public static class CountryCatalog
{
    public static IReadOnlyList<(string Code, string Name, string Currency, string TimeZone)> Generic { get; } =
    [
        ("AE", "United Arab Emirates", "AED", "Asia/Dubai"), ("SA", "Saudi Arabia", "SAR", "Asia/Riyadh"), ("QA", "Qatar", "QAR", "Asia/Qatar"),
        ("KW", "Kuwait", "KWD", "Asia/Kuwait"), ("BH", "Bahrain", "BHD", "Asia/Bahrain"), ("OM", "Oman", "OMR", "Asia/Muscat"),
        ("PK", "Pakistan", "PKR", "Asia/Karachi"), ("IN", "India", "INR", "Asia/Kolkata"), ("BD", "Bangladesh", "BDT", "Asia/Dhaka"),
        ("LK", "Sri Lanka", "LKR", "Asia/Colombo"), ("NP", "Nepal", "NPR", "Asia/Kathmandu"), ("EG", "Egypt", "EGP", "Africa/Cairo"),
        ("JO", "Jordan", "JOD", "Asia/Amman"), ("TR", "Turkey", "TRY", "Europe/Istanbul"), ("GB", "United Kingdom", "GBP", "Europe/London"),
        ("IE", "Ireland", "EUR", "Europe/Dublin"), ("FR", "France", "EUR", "Europe/Paris"), ("DE", "Germany", "EUR", "Europe/Berlin"),
        ("ES", "Spain", "EUR", "Europe/Madrid"), ("IT", "Italy", "EUR", "Europe/Rome"), ("NL", "Netherlands", "EUR", "Europe/Amsterdam"),
        ("US", "United States", "USD", "America/New_York"), ("CA", "Canada", "CAD", "America/Toronto"), ("AU", "Australia", "AUD", "Australia/Sydney"),
        ("SG", "Singapore", "SGD", "Asia/Singapore"), ("MY", "Malaysia", "MYR", "Asia/Kuala_Lumpur"), ("ZA", "South Africa", "ZAR", "Africa/Johannesburg"),
        ("NG", "Nigeria", "NGN", "Africa/Lagos"), ("KE", "Kenya", "KES", "Africa/Nairobi"),
    ];
}

public sealed record ListCountries : IQuery<IReadOnlyList<CountryDto>>;
public sealed record ListCurrencies : IQuery<IReadOnlyList<CurrencyOptionDto>>;

internal sealed class CountryHandlers(CountryPackRegistry packs) :
    IRequestHandler<ListCountries, IReadOnlyList<CountryDto>>,
    IRequestHandler<ListCurrencies, IReadOnlyList<CurrencyOptionDto>>
{
    /// <summary>Installed packs win over the generic data (their defaults come from the pack); the rest keep the neutral entry.</summary>
    public Task<IReadOnlyList<CountryDto>> Handle(ListCountries q, CancellationToken ct)
    {
        var result = CountryCatalog.Generic
            .Where(c => packs.Find(c.Code) is null)
            .Select(c => new CountryDto(c.Code, c.Name, c.Currency, c.TimeZone, false))
            .Concat(packs.All.Select(p => new CountryDto(p.CountryCode, p.Name, p.DefaultCurrency, p.DefaultTimeZone, true)))
            .OrderBy(c => c.Name).ToList();
        return Task.FromResult<IReadOnlyList<CountryDto>>(result);
    }

    public Task<IReadOnlyList<CurrencyOptionDto>> Handle(ListCurrencies q, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CurrencyOptionDto>>(TaxTemplates.Currencies.Select(c => new CurrencyOptionDto(c.Code, c.Name, c.Symbol, c.Decimals)).ToList());
}
