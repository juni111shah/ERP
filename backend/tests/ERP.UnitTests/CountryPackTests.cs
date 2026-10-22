using ERP.Domain.Finance;
using System.Text.RegularExpressions;
using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.CountryPacks.AE;

namespace ERP.UnitTests;

public class CountryPackTests
{
    private static readonly UaeCountryPack Uae = new();

    [Fact]
    public void Registry_finds_packs_case_insensitively_and_returns_null_for_other_countries()
    {
        var registry = new CountryPackRegistry([Uae]);
        Assert.Same(Uae, registry.Find("ae"));
        Assert.Same(Uae, registry.Find("AE"));
        Assert.Null(registry.Find("PK"));
        Assert.Null(registry.Find(null));
    }

    [Fact]
    public void Uae_custom_fields_have_valid_keys_unique_keys_and_compilable_patterns()
    {
        Assert.Equal(Uae.CustomFields.Count, Uae.CustomFields.Select(f => (f.EntityType, f.Key)).Distinct().Count());
        foreach (var f in Uae.CustomFields)
        {
            Assert.Matches(CustomFieldValidator.KeyFormat, f.Key);
            Assert.StartsWith("cf_ae_", f.Key);
            if (f.Options?.Pattern is { } p) _ = new Regex(p);
        }
    }

    [Fact]
    public void Uae_identifier_patterns_accept_real_formats_and_reject_malformed_values()
    {
        var trn = new Regex(Uae.CustomFields.Single(f => f.EntityType == "party" && f.Key == "cf_ae_trn").Options!.Pattern!);
        Assert.Matches(trn, "100123456700003");
        Assert.DoesNotMatch(trn, "12345");
        var eid = new Regex(Uae.CustomFields.Single(f => f.EntityType == "party" && f.Key == "cf_ae_emirates_id").Options!.Pattern!);
        Assert.Matches(eid, "784-1990-1234567-1");
        Assert.DoesNotMatch(eid, "785-1990-1234567-1");
    }

    [Fact]
    public void Uae_pack_has_exactly_one_default_tax_at_five_percent()
    {
        var def = Assert.Single(Uae.Taxes, t => t.IsDefault);
        Assert.Equal(5m, def.Rate);
        Assert.Equal("VAT5", def.Code);
    }

    [Fact]
    public void Core_fallback_taxes_no_longer_hard_code_the_uae()
        => Assert.DoesNotContain(TaxTemplates.ForCountry("AE"), t => t.Code == "VAT5");
}

public class AllCountryPackTests
{
    public static IEnumerable<object[]> Packs() => CountryPackCatalog.All().Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Packs))]
    public void Every_pack_is_well_formed(ICountryPack pack)
    {
        Assert.Matches("^[A-Z]{2}$", pack.CountryCode);
        Assert.Matches("^[A-Z]{3}$", pack.DefaultCurrency);
        Assert.False(string.IsNullOrWhiteSpace(pack.DefaultTimeZone));
        var defaults = pack.Taxes.Where(t => t.IsDefault).ToList();
        Assert.Single(defaults); // exactly one default tax code
        Assert.Equal(pack.Taxes.Count, pack.Taxes.Select(t => t.Code).Distinct().Count());
        var prefix = $"cf_{pack.CountryCode.ToLowerInvariant()}_";
        Assert.Equal(pack.CustomFields.Count, pack.CustomFields.Select(f => f.EntityType + f.Key).Distinct().Count());
        foreach (var f in pack.CustomFields)
        {
            Assert.StartsWith(prefix, f.Key); // pack fields are namespaced by country
            Assert.Matches(CustomFieldValidator.KeyFormat, f.Key);
            if (f.Options?.Pattern is { } pattern) _ = new Regex(pattern);
        }
    }

    [Fact]
    public void Registry_resolves_every_installed_country_and_core_has_no_country_rules()
    {
        var registry = new CountryPackRegistry(CountryPackCatalog.All());
        foreach (var code in new[] { "AE", "SA", "PK", "IN", "GB", "US" }) Assert.NotNull(registry.Find(code));
        Assert.Null(registry.Find("FR"));
        foreach (var code in new[] { "AE", "SA", "PK", "IN", "GB", "US" })
            Assert.Equal(["STD", "EXEMPT"], TaxTemplates.ForCountry(code).Select(t => t.Code)); // core fallback is country-neutral
    }

    [Theory]
    [InlineData("SA", "cf_sa_vat_no", "300000000000003", "123")]
    [InlineData("PK", "cf_pk_cnic", "35202-1234567-1", "3520212345671")]
    [InlineData("IN", "cf_in_gstin", "27ABCDE1234F1Z5", "27abcde")]
    [InlineData("IN", "cf_in_pan", "ABCDE1234F", "ABCDE12345")]
    [InlineData("GB", "cf_gb_vat_no", "GB123456789", "12345")]
    [InlineData("US", "cf_us_ein", "12-3456789", "123456789")]
    public void Identifier_patterns_accept_valid_and_reject_malformed_values(string country, string key, string valid, string invalid)
    {
        var field = CountryPackCatalog.All().Single(p => p.CountryCode == country).CustomFields.Single(f => f.Key == key);
        var regex = new Regex(field.Options!.Pattern!);
        Assert.Matches(regex, valid);
        Assert.DoesNotMatch(regex, invalid);
    }
}
