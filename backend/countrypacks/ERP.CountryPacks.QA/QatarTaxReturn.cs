using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.QA;

/// <summary>
/// Qatar levies no VAT, so there is no VAT return: this is an information-only turnover summary (sales, exempt sales, purchases) and the tax
/// payable is always zero. Corporate income tax is filed with the General Tax Authority outside the ERP.
/// </summary>
internal static class QatarTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("QA-TURNOVER", "Qatar turnover summary (no VAT)", "ملخص المبيعات (لا توجد ضريبة قيمة مضافة)",
    [
        Sum("1_sales", "1. Total sales", "١. إجمالي المبيعات", l => l.Side == TaxSide.Sales),
        Sum("2_exempt", "2. Of which exempt sales", "٢. منها مبيعات معفاة", l => l.Side == TaxSide.Sales && l.IsExempt),
        Sum("3_purchases", "3. Total purchases", "٣. إجمالي المشتريات", l => l.Side == TaxSide.Purchases),
        Calc("net_due", "4. Tax payable (no VAT in Qatar)", "٤. الضريبة المستحقة (لا توجد ضريبة قيمة مضافة في قطر)", _ => 0m, total: true),
    ], "net_due", Notes: "Information only: Qatar has no VAT.");
}
