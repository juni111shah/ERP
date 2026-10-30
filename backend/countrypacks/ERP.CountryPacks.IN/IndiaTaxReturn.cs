using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.IN;

/// <summary>GSTR-3B summary: table 3.1 outward supplies and table 4 input tax credit. CGST/SGST/IGST are not split: tax is combined.</summary>
internal static class IndiaTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("GSTR-3B", "GST return summary (GSTR-3B)", "ملخص إقرار ضريبة السلع والخدمات (GSTR-3B)",
    [
        Sum("3_1_a_value", "3.1(a) Outward taxable supplies: taxable value", "٣.١(أ) التوريدات الخارجية الخاضعة: القيمة", l => l.Side == TaxSide.Sales && l.IsStandard),
        Sum("3_1_a_tax", "3.1(a) Outward taxable supplies: tax", "٣.١(أ) التوريدات الخارجية الخاضعة: الضريبة", l => l.Side == TaxSide.Sales && l.IsStandard, TaxMeasure.Tax),
        Sum("3_1_b_value", "3.1(b) Zero-rated supplies: taxable value", "٣.١(ب) التوريدات بنسبة الصفر", l => l.Side == TaxSide.Sales && l.IsZeroRated),
        Sum("3_1_c_value", "3.1(c) Nil-rated and exempt supplies", "٣.١(ج) التوريدات المعفاة", l => l.Side == TaxSide.Sales && l.IsExempt),
        Sum("4_itc", "4(A) Input tax credit available", "٤(أ) رصيد ضريبة المدخلات المتاح", l => l.Side == TaxSide.Purchases && l.Recoverable, TaxMeasure.Tax),
        Calc("net_due", "Net tax payable (negative = credit carried forward)", "صافي الضريبة المستحقة (سالب = رصيد مرحّل)", v => v.V("3_1_a_tax") - v.V("4_itc"), total: true),
    ], "net_due", Notes: "Simplified: combined tax only (no CGST/SGST/IGST split), no reverse-charge or import ITC tables.");
}
