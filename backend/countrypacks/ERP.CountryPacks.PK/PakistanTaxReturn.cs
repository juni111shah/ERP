using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.PK;

/// <summary>Sales tax return summary: taxable sales, zero-rated and exempt sales, input tax and the net payable.</summary>
internal static class PakistanTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("STR-PK", "Sales tax return summary", "ملخص إقرار ضريبة المبيعات",
    [
        Sum("sales_value", "Taxable sales: value", "المبيعات الخاضعة: القيمة", l => l.Side == TaxSide.Sales && l.IsStandard),
        Sum("sales_tax", "Taxable sales: sales tax", "المبيعات الخاضعة: الضريبة", l => l.Side == TaxSide.Sales && l.IsStandard, TaxMeasure.Tax),
        Sum("zero_value", "Zero-rated sales", "المبيعات بنسبة الصفر", l => l.Side == TaxSide.Sales && l.IsZeroRated),
        Sum("exempt_value", "Exempt sales", "المبيعات المعفاة", l => l.Side == TaxSide.Sales && l.IsExempt),
        Sum("input_value", "Purchases with input tax: value", "المشتريات ذات ضريبة المدخلات: القيمة", l => l.Side == TaxSide.Purchases && l.Recoverable && l.Rate > 0),
        Sum("input_tax", "Input tax admissible", "ضريبة المدخلات المقبولة", l => l.Side == TaxSide.Purchases && l.Recoverable, TaxMeasure.Tax),
        Calc("net_due", "Net sales tax payable (negative = refundable)", "صافي ضريبة المبيعات المستحقة (سالب = مستردّة)", v => v.V("sales_tax") - v.V("input_tax"), total: true),
    ], "net_due", Notes: "Simplified: no further-tax or withholding adjustments, no annexure-level detail.");
}
