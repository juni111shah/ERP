using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.QA;

/// <summary>Qatar levies no VAT, so the "return" is an information summary of sales and purchases with a nil amount due.</summary>
internal static class QatarTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("QA-SUMMARY", "Qatar sales and purchases summary (no VAT)", "ملخص المبيعات والمشتريات في قطر (لا توجد ضريبة قيمة مضافة)",
    [
        Sum("1_amount", "1. Total sales", "١. إجمالي المبيعات", l => l.Side == TaxSide.Sales),
        Sum("2_amount", "2. Total purchases", "٢. إجمالي المشتريات", l => l.Side == TaxSide.Purchases),
        Calc("net_due", "3. Net VAT due (always nil: Qatar levies no VAT)", "٣. صافي الضريبة المستحقة (صفر دائماً: لا تفرض قطر ضريبة قيمة مضافة)", v => 0m, total: true),
    ], "net_due", Notes: "Information only. Add a tax code and a return definition here if Qatar introduces VAT.");
}
