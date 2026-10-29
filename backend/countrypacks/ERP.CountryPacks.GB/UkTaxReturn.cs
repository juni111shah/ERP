using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.GB;

/// <summary>UK VAT return (VAT100), nine boxes. Boxes 2, 8 and 9 (EU acquisitions and supplies) are zero: the data model has no EU flag.</summary>
internal static class UkTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("VAT100", "UK VAT return (VAT100)", "إقرار ضريبة القيمة المضافة (المملكة المتحدة)",
    [
        Sum("box1", "Box 1. VAT due on sales", "المربع ١. ضريبة المبيعات المستحقة", l => l.Side == TaxSide.Sales, TaxMeasure.Tax),
        Calc("box2", "Box 2. VAT due on acquisitions from the EU", "المربع ٢. ضريبة الاستحواذات من الاتحاد الأوروبي", _ => 0m),
        Calc("box3", "Box 3. Total VAT due", "المربع ٣. إجمالي الضريبة المستحقة", v => v.V("box1") + v.V("box2"), total: true),
        Sum("box4", "Box 4. VAT reclaimed on purchases", "المربع ٤. ضريبة المشتريات المستردّة", l => l.Side == TaxSide.Purchases && l.Recoverable, TaxMeasure.Tax),
        Calc("net_due", "Box 5. Net VAT to pay (negative = reclaim)", "المربع ٥. صافي الضريبة المستحقة (سالب = استرداد)", v => v.V("box3") - v.V("box4"), total: true),
        Sum("box6", "Box 6. Total sales excluding VAT", "المربع ٦. إجمالي المبيعات بدون الضريبة", l => l.Side == TaxSide.Sales),
        Sum("box7", "Box 7. Total purchases excluding VAT", "المربع ٧. إجمالي المشتريات بدون الضريبة", l => l.Side == TaxSide.Purchases),
        Calc("box8", "Box 8. Supplies to the EU excluding VAT", "المربع ٨. التوريدات إلى الاتحاد الأوروبي", _ => 0m),
        Calc("box9", "Box 9. Acquisitions from the EU excluding VAT", "المربع ٩. الاستحواذات من الاتحاد الأوروبي", _ => 0m),
    ], "net_due", Notes: "Simplified: EU boxes (2, 8, 9) are not modelled and show zero. Box 5 is shown signed.");
}
