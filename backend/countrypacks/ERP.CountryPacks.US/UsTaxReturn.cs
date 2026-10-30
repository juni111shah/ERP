using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.US;

/// <summary>
/// Sales tax summary by rate. The US has no federal return: states and local authorities each have their own forms, so this lists taxable and exempt
/// sales and the tax collected per rate, to be keyed into the relevant state return. Purchases are not part of it.
/// </summary>
internal static class UsTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("SALES-TAX-SUMMARY", "Sales tax summary by rate", "ملخص ضريبة المبيعات حسب النسبة",
    [
        Sum("taxable_sales", "Total taxable sales", "إجمالي المبيعات الخاضعة", l => l.Side == TaxSide.Sales && l.Rate > 0),
        Sum("exempt_sales", "Exempt and non-taxable sales", "المبيعات المعفاة وغير الخاضعة", l => l.Side == TaxSide.Sales && l.Rate == 0),
        Sum("tax_collected", "Total sales tax collected", "إجمالي ضريبة المبيعات المحصّلة", l => l.Side == TaxSide.Sales, TaxMeasure.Tax),
        Calc("net_due", "Sales tax payable", "ضريبة المبيعات المستحقة", v => v.V("tax_collected"), total: true),
    ], "net_due",
    Dynamic: lines => lines.Where(l => l.Side == TaxSide.Sales && l.Rate > 0).Select(l => l.Rate).Distinct().OrderBy(r => r).SelectMany(rate => new[]
    {
        Sum($"rate_{rate}_sales", $"Taxable sales at {rate:0.####}%", $"المبيعات الخاضعة بنسبة {rate:0.####}%", l => l.Side == TaxSide.Sales && l.Rate == rate),
        Sum($"rate_{rate}_tax", $"Tax collected at {rate:0.####}%", $"الضريبة المحصّلة بنسبة {rate:0.####}%", l => l.Side == TaxSide.Sales && l.Rate == rate, TaxMeasure.Tax),
    }).ToList(),
    Notes: "No federal return exists. This summary feeds state and local returns, which are not modelled.");
}
