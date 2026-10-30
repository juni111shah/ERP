using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.AE;

/// <summary>
/// UAE VAT return (Form 201), simplified: supplies in all emirates are collapsed into the standard-rated line (boxes 1a-1g), and the
/// reverse-charge box uses tax codes whose code starts with "RC". Tourist refund and imports boxes are not modelled.
/// </summary>
internal static class UaeTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("VAT201", "UAE VAT return (Form 201)", "إقرار ضريبة القيمة المضافة (نموذج 201)",
    [
        Sum("1_amount", "1. Standard-rated supplies: amount", "١. التوريدات الخاضعة للنسبة الأساسية: القيمة", l => l.Side == TaxSide.Sales && l.IsStandard),
        Sum("1_vat", "1. Standard-rated supplies: VAT", "١. التوريدات الخاضعة للنسبة الأساسية: الضريبة", l => l.Side == TaxSide.Sales && l.IsStandard, TaxMeasure.Tax),
        Sum("3_amount", "3. Reverse-charge supplies: amount", "٣. التوريدات الخاضعة للاحتساب العكسي: القيمة", l => l.Side == TaxSide.Purchases && l.IsReverseCharge),
        Sum("3_vat", "3. Reverse-charge supplies: VAT", "٣. التوريدات الخاضعة للاحتساب العكسي: الضريبة", l => l.Side == TaxSide.Purchases && l.IsReverseCharge, TaxMeasure.Tax),
        Sum("4_amount", "4. Zero-rated supplies", "٤. التوريدات الخاضعة لنسبة الصفر", l => l.Side == TaxSide.Sales && l.IsZeroRated),
        Sum("5_amount", "5. Exempt supplies", "٥. التوريدات المعفاة", l => l.Side == TaxSide.Sales && l.IsExempt),
        Calc("8_vat", "8. Total output VAT", "٨. إجمالي ضريبة المخرجات", v => v.V("1_vat") + v.V("3_vat"), total: true),
        Sum("9_amount", "9. Standard-rated expenses: amount", "٩. المصروفات الخاضعة للنسبة الأساسية: القيمة", l => l.Side == TaxSide.Purchases && l.IsStandard && l.Recoverable),
        Sum("9_vat", "9. Standard-rated expenses: recoverable VAT", "٩. المصروفات الخاضعة للنسبة الأساسية: الضريبة القابلة للاسترداد", l => l.Side == TaxSide.Purchases && l.IsStandard && l.Recoverable, TaxMeasure.Tax),
        Calc("10_vat", "10. Reverse-charge input VAT", "١٠. ضريبة المدخلات للاحتساب العكسي", v => v.V("3_vat")),
        Calc("11_vat", "11. Total input VAT", "١١. إجمالي ضريبة المدخلات", v => v.V("9_vat") + v.V("10_vat"), total: true),
        Calc("net_due", "14. Net VAT due (negative = refundable)", "١٤. صافي الضريبة المستحقة (سالب = مستردّة)", v => v.V("8_vat") - v.V("11_vat"), total: true),
    ], "net_due", Notes: "Simplified: emirate split (1a-1g), tourist refunds and import boxes are not modelled.");
}
