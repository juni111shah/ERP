using ERP.Application.Templates;
using ERP.CountryPacks.AE;
using ERP.CountryPacks.GB;
using ERP.CountryPacks.IN;
using ERP.CountryPacks.PK;
using ERP.CountryPacks.SA;
using ERP.CountryPacks.US;
using ERP.Domain.Finance;

namespace ERP.UnitTests;

/// <summary>Each country's return is data: fabricated tax lines in, box values out.</summary>
public class TaxReturnTests
{
    private static TaxLine Sale(string code, decimal rate, decimal net, decimal tax, bool recoverable = true) => new(TaxSide.Sales, code, TaxKind.Vat, rate, recoverable, net, tax);
    private static TaxLine Buy(string code, decimal rate, decimal net, decimal tax, bool recoverable = true) => new(TaxSide.Purchases, code, TaxKind.Vat, rate, recoverable, net, tax);

    private static decimal Box(IReadOnlyList<TaxReturnRow> rows, string key) => rows.Single(r => r.Key == key).Amount;

    private static readonly TaxLine[] Typical =
    [
        Sale("STD", 5m, 1000m, 50m), Sale("ZERO", 0m, 400m, 0m), Sale("EXEMPT", 0m, 300m, 0m, recoverable: false), Sale("STD", 5m, -200m, -10m), // a credit note nets off
        Buy("STD", 5m, 600m, 30m), Buy("STD", 5m, 100m, 5m, recoverable: false), Buy("RC5", 5m, 200m, 10m),
    ];

    [Fact]
    public void Uae_return_splits_standard_zero_exempt_and_reverse_charge()
    {
        var (rows, net) = TaxReturnCalculator.Compute(UaeCountryPack_Definition(), Typical);
        Assert.Equal(800m, Box(rows, "1_amount")); Assert.Equal(40m, Box(rows, "1_vat"));
        Assert.Equal(400m, Box(rows, "4_amount")); Assert.Equal(300m, Box(rows, "5_amount"));
        Assert.Equal(200m, Box(rows, "3_amount")); Assert.Equal(10m, Box(rows, "3_vat"));
        Assert.Equal(50m, Box(rows, "8_vat"));
        Assert.Equal(600m, Box(rows, "9_amount")); Assert.Equal(30m, Box(rows, "9_vat")); // the non-recoverable purchase is excluded
        Assert.Equal(40m, Box(rows, "11_vat"));
        Assert.Equal(10m, net);
    }

    [Fact]
    public void Saudi_return_nets_output_against_input()
    {
        var (rows, net) = TaxReturnCalculator.Compute(new SaudiCountryPack().TaxReturn!, Typical);
        Assert.Equal(40m, Box(rows, "6_vat")); Assert.Equal(30m, Box(rows, "12_vat")); Assert.Equal(10m, net);
    }

    [Fact]
    public void Uk_return_has_the_nine_boxes_and_a_signed_net()
    {
        var def = new UnitedKingdomCountryPack().TaxReturn!;
        var (rows, net) = TaxReturnCalculator.Compute(def, [Sale("S", 20m, 1000m, 200m), Buy("S", 20m, 500m, 100m)]);
        Assert.Equal(9, rows.Count(r => r.Key.StartsWith("box") || r.Key == "net_due"));
        Assert.Equal(200m, Box(rows, "box1")); Assert.Equal(200m, Box(rows, "box3")); Assert.Equal(100m, Box(rows, "box4")); Assert.Equal(100m, net);
        Assert.Equal(1000m, Box(rows, "box6")); Assert.Equal(500m, Box(rows, "box7"));
        Assert.Equal(0m, Box(rows, "box8"));
        var (_, refund) = TaxReturnCalculator.Compute(def, [Buy("S", 20m, 500m, 100m)]);
        Assert.Equal(-100m, refund);
    }

    [Fact]
    public void India_summary_reports_outward_supplies_and_itc()
    {
        var (rows, net) = TaxReturnCalculator.Compute(new IndiaCountryPack().TaxReturn!, [Sale("GST18", 18m, 1000m, 180m), Sale("NIL", 0m, 100m, 0m, recoverable: false), Buy("GST18", 18m, 400m, 72m)]);
        Assert.Equal(1000m, Box(rows, "3_1_a_value")); Assert.Equal(180m, Box(rows, "3_1_a_tax")); Assert.Equal(100m, Box(rows, "3_1_c_value")); Assert.Equal(72m, Box(rows, "4_itc"));
        Assert.Equal(108m, net);
    }

    [Fact]
    public void Pakistan_summary_deducts_admissible_input_tax()
    {
        var (rows, net) = TaxReturnCalculator.Compute(new PakistanCountryPack().TaxReturn!, [Sale("ST18", 18m, 1000m, 180m), Buy("ST18", 18m, 500m, 90m), Buy("ST18", 18m, 100m, 18m, recoverable: false)]);
        Assert.Equal(90m, Box(rows, "input_tax")); Assert.Equal(90m, net); Assert.Equal(500m, Box(rows, "input_value"));
    }

    [Fact]
    public void Us_summary_lists_one_pair_of_rows_per_rate_and_ignores_purchases()
    {
        var (rows, net) = TaxReturnCalculator.Compute(new UnitedStatesCountryPack().TaxReturn!, [Sale("CA", 7.25m, 1000m, 72.5m), Sale("NY", 8.875m, 200m, 17.75m), Sale("NONE", 0m, 50m, 0m), Buy("X", 5m, 99m, 5m)]);
        Assert.Equal(1000m, Box(rows, "rate_7.25_sales")); Assert.Equal(72.5m, Box(rows, "rate_7.25_tax")); Assert.Equal(17.75m, Box(rows, "rate_8.875_tax"));
        Assert.Equal(1200m, Box(rows, "taxable_sales")); Assert.Equal(50m, Box(rows, "exempt_sales")); Assert.Equal(90.25m, net);
        Assert.Equal("rate_7.25_sales", rows[0].Key); // dynamic rows come first
    }

    [Fact]
    public void Every_pack_defines_a_return_with_unique_box_keys_a_net_box_and_arabic_labels()
    {
        foreach (var pack in CountryPackCatalog.All())
        {
            var def = pack.TaxReturn;
            Assert.NotNull(def);
            Assert.Equal(def!.Boxes.Count, def.Boxes.Select(b => b.Key).Distinct().Count());
            Assert.Contains(def.Boxes, b => b.Key == def.NetPayableKey);
            Assert.All(def.Boxes, b => { Assert.False(string.IsNullOrWhiteSpace(b.Label)); Assert.False(string.IsNullOrWhiteSpace(b.LabelAr)); });
            Assert.False(string.IsNullOrWhiteSpace(def.TitleAr));
            var (rows, _) = TaxReturnCalculator.Compute(def, []); // an empty period is all zeros, never an error
            Assert.All(rows, r => Assert.Equal(0m, r.Amount));
        }
    }

    [Fact]
    public void Tax_lines_classify_zero_rated_exempt_and_reverse_charge()
    {
        Assert.True(Sale("Z", 0m, 1m, 0m).IsZeroRated);
        Assert.True(Sale("E", 0m, 1m, 0m, recoverable: false).IsExempt);
        Assert.True(Buy("RC5", 5m, 1m, 0.05m).IsReverseCharge);
        Assert.False(Buy("RC5", 5m, 1m, 0.05m).IsStandard);
        Assert.True(Sale("V5", 5m, 1m, 0.05m).IsStandard);
    }

    private static TaxReturnDefinition UaeCountryPack_Definition() => new UaeCountryPack().TaxReturn!;
}
