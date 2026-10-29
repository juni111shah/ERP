using ERP.Application.Customization;
using ERP.Domain.Customization;

namespace ERP.Application.Templates;

/// <summary>A custom field a country pack provisions for a workspace of that country (an ordinary, editable definition afterwards).</summary>
public sealed record CustomFieldSeed(
    string EntityType, string Key, string Label, CustomFieldDataType DataType, bool Required = false,
    CustomFieldOptions? Options = null, string? Section = null, string? HelpText = null, int Order = 0,
    bool IsSearchable = false, bool ShowInList = false);

/// <summary>
/// Country rules live in packs, never in the core: tax codes, country-specific custom fields and (later) statutory
/// reports. A pack is applied when a workspace is created with its country code.
/// </summary>
/// <summary>How a country computes end-of-service benefit: entitlement days for a number of service years (the amount is basic ÷ 30 × days).</summary>
public sealed record EndOfServiceRule(string Description, Func<decimal, decimal> EntitlementDays, decimal MinimumYears = 1m);

public interface ICountryPack
{
    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    string CountryCode { get; }
    string Name { get; }
    /// <summary>Suggested base currency and time zone at onboarding (the user can still choose another).</summary>
    string DefaultCurrency { get; }
    string DefaultTimeZone { get; }
    IReadOnlyList<TaxTemplate> Taxes { get; }
    IReadOnlyList<CustomFieldSeed> CustomFields { get; }
    /// <summary>The country's end-of-service rule, when it has one (used by HR); null = none.</summary>
    EndOfServiceRule? EndOfService => null;
    /// <summary>The country's periodic tax return (VAT return and the like), when the pack describes one; null = none.</summary>
    TaxReturnDefinition? TaxReturn => null;
    /// <summary>Items the country expects on a tax invoice; checked (as warnings only) when an invoice with tax is issued.</summary>
    IReadOnlyList<InvoiceField> InvoiceRequiredFields => [];
}

/// <summary>Installed country packs, looked up by country code. Countries without a pack get the neutral core defaults.</summary>
public sealed class CountryPackRegistry(IEnumerable<ICountryPack> packs)
{
    private readonly Dictionary<string, ICountryPack> byCode = packs.ToDictionary(p => p.CountryCode.ToUpperInvariant());

    public IReadOnlyCollection<ICountryPack> All => byCode.Values;

    public ICountryPack? Find(string? countryCode)
        => countryCode is not null && byCode.TryGetValue(countryCode.ToUpperInvariant(), out var pack) ? pack : null;
}
