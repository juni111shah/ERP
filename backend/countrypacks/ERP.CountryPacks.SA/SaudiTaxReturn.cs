using ERP.Application.Templates;
using static ERP.Application.Templates.TaxBoxes;

namespace ERP.CountryPacks.SA;

/// <summary>Saudi VAT return, simplified to the main sales and purchases boxes. Exports and imports are not separated from zero-rated and standard purchases.</summary>
internal static class SaudiTaxReturn
{
    public static TaxReturnDefinition Definition { get; } = new("VAT-SA", "Saudi VAT return", "إقرار ضريبة القيمة المضافة (السعودية)",
    [
        Sum("1_amount", "1. Standard-rated sales: amount", "١. المبيعات الخاضعة للنسبة الأساسية: القيمة", l => l.Side == TaxSide.Sales && l.IsStandard),
        Sum("1_vat", "1. Standard-rated sales: VAT", "١. المبيعات الخاضعة للنسبة الأساسية: الضريبة", l => l.Side == TaxSide.Sales && l.IsStandard, TaxMeasure.Tax),
        Sum("4_amount", "4. Zero-rated sales", "٤. المبيعات الخاضعة لنسبة الصفر", l => l.Side == TaxSide.Sales && l.IsZeroRated),
        Sum("5_amount", "5. Exempt sales", "٥. المبيعات المعفاة", l => l.Side == TaxSide.Sales && l.IsExempt),
        Calc("6_vat", "6. Total output VAT", "٦. إجمالي ضريبة المخرجات", v => v.V("1_vat"), total: true),
        Sum("9_amount", "9. Standard-rated domestic purchases: amount", "٩. المشتريات المحلية الخاضعة للنسبة الأساسية: القيمة", l => l.Side == TaxSide.Purchases && l.IsStandard && l.Recoverable),
        Sum("9_vat", "9. Standard-rated domestic purchases: VAT", "٩. المشتريات المحلية الخاضعة للنسبة الأساسية: الضريبة", l => l.Side == TaxSide.Purchases && l.IsStandard && l.Recoverable, TaxMeasure.Tax),
        Calc("12_vat", "12. Total input VAT", "١٢. إجمالي ضريبة المدخلات", v => v.V("9_vat"), total: true),
        Calc("net_due", "15. Net VAT due (negative = refundable)", "١٥. صافي الضريبة المستحقة (سالب = مستردّة)", v => v.V("6_vat") - v.V("12_vat"), total: true),
    ], "net_due", Notes: "Simplified: imports, reverse charge and exports are not separated.");
}
