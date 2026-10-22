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
public interface ICountryPack
{
    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    string CountryCode { get; }
    string Name { get; }
    IReadOnlyList<TaxTemplate> Taxes { get; }
    IReadOnlyList<CustomFieldSeed> CustomFields { get; }
}

/// <summary>Installed country packs, looked up by country code. Countries without a pack get the neutral core defaults.</summary>
public sealed class CountryPackRegistry(IEnumerable<ICountryPack> packs)
{
    private readonly Dictionary<string, ICountryPack> byCode = packs.ToDictionary(p => p.CountryCode.ToUpperInvariant());

    public IReadOnlyCollection<ICountryPack> All => byCode.Values;

    public ICountryPack? Find(string? countryCode)
        => countryCode is not null && byCode.TryGetValue(countryCode.ToUpperInvariant(), out var pack) ? pack : null;
}
