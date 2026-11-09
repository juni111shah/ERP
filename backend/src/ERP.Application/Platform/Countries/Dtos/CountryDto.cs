namespace ERP.Application.Platform;

/// <summary>A country offered at onboarding. <c>HasPack</c> = a country pack supplies tax codes and country-specific fields.</summary>
public sealed record CountryDto(string Code, string Name, string DefaultCurrency, string DefaultTimeZone, bool HasPack);
public sealed record CurrencyOptionDto(string Code, string Name, string Symbol, int Decimals);
