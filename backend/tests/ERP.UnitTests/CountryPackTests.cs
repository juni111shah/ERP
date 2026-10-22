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
        Assert.Equal(Uae.CustomFields.Count, Uae.CustomFields.Select(f => f.Key).Distinct().Count());
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
        var trn = new Regex(Uae.CustomFields.Single(f => f.Key == "cf_ae_trn").Options!.Pattern!);
        Assert.Matches(trn, "100123456700003");
        Assert.DoesNotMatch(trn, "12345");
        var eid = new Regex(Uae.CustomFields.Single(f => f.Key == "cf_ae_emirates_id").Options!.Pattern!);
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
