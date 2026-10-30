using System.Text.RegularExpressions;
using ERP.Application.Customization;
using ERP.Application.Templates;
using ERP.CountryPacks.QA;
using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class QatarCountryPackTests
{
    private static readonly QatarCountryPack Qatar = new();

    [Fact]
    public void The_pack_defaults_to_qatar_riyal_and_doha_time_and_is_found_by_the_registry()
    {
        Assert.Equal(("QA", "QAR", "Asia/Qatar"), (Qatar.CountryCode, Qatar.DefaultCurrency, Qatar.DefaultTimeZone));
        Assert.Same(Qatar, new CountryPackRegistry([Qatar]).Find("qa"));
    }

    [Fact]
    public void Qatar_has_no_vat_so_the_only_default_tax_is_zero_percent()
    {
        var def = Assert.Single(Qatar.Taxes, t => t.IsDefault);
        Assert.Equal(("NOVAT", 0m), (def.Code, def.Rate));
        Assert.All(Qatar.Taxes, t => Assert.Equal(0m, t.Rate));
        Assert.Contains(Qatar.Taxes, t => t.Code == "EXEMPT" && !t.IsRecoverable);
        var ret = Assert.IsType<TaxReturnDefinition>(((ICountryPack)Qatar).TaxReturn);
        Assert.Equal(0m, TaxReturnCalculator.Compute(ret, []).NetPayable); // no VAT: nothing is ever payable
    }

    [Fact]
    public void Custom_fields_have_valid_unique_keys_and_compilable_patterns()
    {
        Assert.Equal(Qatar.CustomFields.Count, Qatar.CustomFields.Select(f => (f.EntityType, f.Key)).Distinct().Count());
        foreach (var f in Qatar.CustomFields)
        {
            Assert.Matches(CustomFieldValidator.KeyFormat, f.Key);
            Assert.StartsWith("cf_qa_", f.Key);
            if (f.Options?.Pattern is { } p) _ = new Regex(p);
        }
    }

    [Fact]
    public void Identifier_patterns_accept_real_formats_and_reject_malformed_values()
    {
        var qid = new Regex(Qatar.CustomFields.Single(f => f.EntityType == "hr.employee" && f.Key == "cf_qa_qid").Options!.Pattern!);
        Assert.Matches(qid, "29850123456");
        Assert.DoesNotMatch(qid, "2985012345");
        var wps = new Regex(Qatar.CustomFields.Single(f => f.Key == "cf_qa_wps_bank_short_code").Options!.Pattern!);
        Assert.Matches(wps, "QNBA");
        Assert.DoesNotMatch(wps, "qnba1");
    }

    [InlineData(1, 21, 4_200)]   // 6,000 / 30 x 21
    [InlineData(5, 105, 21_000)]
    [InlineData(10, 210, 42_000)] // no step-up after five years, unlike the UAE
    [Theory]
    public void The_qatar_rule_gives_21_days_for_each_year_of_service(int years, double days, double amount)
    {
        var calc = new GratuityCalculator(new CountryPackRegistry([Qatar]));
        var asOf = new DateOnly(2026, 10, 22);
        var g = calc.Calculate(6_000m, asOf.AddYears(-years).AddDays(-3), null, asOf, "QA");
        Assert.True(g.Eligible);
        Assert.InRange(g.EntitlementDays, (decimal)days, (decimal)days + 1.5m);
        Assert.InRange(g.Amount, (decimal)amount, (decimal)amount + 300m);
    }

    [Fact]
    public void Under_a_year_of_service_there_is_no_entitlement()
    {
        var asOf = new DateOnly(2026, 10, 22);
        var g = new GratuityCalculator(new CountryPackRegistry([Qatar])).Calculate(6_000m, asOf.AddMonths(-6), null, asOf, "QA");
        Assert.Equal((false, 0m), (g.Eligible, g.Amount));
    }
}
