import 'package:flutter/widgets.dart';

// Plain-language captions for accounting jargon.
//
// MOBILE UI CAPTIONS ONLY: screen titles, field labels, tooltips, confirm text written in the app.
// Never run server data through this (KPI labels, chart/alert titles, account or section names,
// status codes, permission keys, query values, Option values): those come from the server and
// must stay exactly as sent. See JARGON_USAGES.md for which call sites are safe to swap.

/// English + Arabic plain wording for one jargon term.
@immutable
class PlainLabel {
  const PlainLabel(this.en, this.ar);

  final String en;
  final String ar;

  String forLanguage(String languageCode) => languageCode == 'ar' ? ar : en;
}

abstract final class PlainLabels {
  /// Jargon (as written in English captions) -> plain wording. Keys are matched case-insensitively
  /// and with surrounding whitespace trimmed. Debit / Credit are deliberately absent: they are
  /// standard terms with no shorter plain equivalent, so keep them as-is.
  static const Map<String, PlainLabel> terms = {
    // Money in / money out.
    'Receivables': PlainLabel('Money owed to you', 'أموال مستحقة لك'),
    'Accounts receivable': PlainLabel('Money owed to you', 'أموال مستحقة لك'),
    'AR': PlainLabel('Money owed to you', 'أموال مستحقة لك'),
    'Receivable balance': PlainLabel('Amount owed to you', 'المبلغ المستحق لك'),
    'Total receivable': PlainLabel('Total owed to you', 'إجمالي المستحق لك'),
    'Receivables ageing': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),
    'Receivables aging': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),
    'AR ageing': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),
    'AR aging': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),
    'Payables': PlainLabel('Money you owe', 'أموال مستحقة عليك'),
    'Accounts payable': PlainLabel('Money you owe', 'أموال مستحقة عليك'),
    'AP': PlainLabel('Money you owe', 'أموال مستحقة عليك'),
    'Payables ageing': PlainLabel('Bills overdue by age', 'الفواتير المتأخرة حسب المدة'),
    'Payables aging': PlainLabel('Bills overdue by age', 'الفواتير المتأخرة حسب المدة'),
    'AP ageing': PlainLabel('Bills overdue by age', 'الفواتير المتأخرة حسب المدة'),
    'Ageing': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),
    'Aging': PlainLabel('Overdue by age', 'المتأخرات حسب المدة'),

    // Books.
    'Ledger': PlainLabel('Accounts', 'الحسابات'),
    'General ledger': PlainLabel('Account history', 'سجل الحسابات'),
    'GL': PlainLabel('Account history', 'سجل الحسابات'),
    'Journal': PlainLabel('Manual entry', 'قيد يدوي'),
    'Journals': PlainLabel('Manual entries', 'قيود يدوية'),
    'Journal entry': PlainLabel('Manual entry', 'قيد يدوي'),
    'Chart of accounts': PlainLabel('Account list', 'قائمة الحسابات'),
    'Trial balance': PlainLabel('Totals per account', 'الأرصدة لكل حساب'),
    'Balance sheet': PlainLabel('What you own and owe', 'ما تملكه وما عليك'),
    'P&L': PlainLabel('Income vs expenses', 'الإيرادات مقابل المصروفات'),
    'Profit and loss': PlainLabel('Income vs expenses', 'الإيرادات مقابل المصروفات'),
    'Profit & loss': PlainLabel('Income vs expenses', 'الإيرادات مقابل المصروفات'),
    'Income statement': PlainLabel('Income vs expenses', 'الإيرادات مقابل المصروفات'),
    'Net profit': PlainLabel('Profit after costs', 'الربح بعد التكاليف'),
    'Liabilities': PlainLabel('What you owe', 'ما عليك'),
    'Total liabilities': PlainLabel('Total you owe', 'إجمالي ما عليك'),
    'Liability': PlainLabel('Something you owe', 'التزام عليك'),
    'Equity': PlainLabel("Owner's share", 'حصة المالك'),
    'Total equity': PlainLabel("Total owner's share", 'إجمالي حصة المالك'),
    'Fiscal year': PlainLabel('Financial year', 'السنة المالية'),
    'KPI': PlainLabel('Key numbers', 'الأرقام الرئيسية'),
    'KPIs': PlainLabel('Key numbers', 'الأرقام الرئيسية'),

    // Actions / states.
    'Post': PlainLabel('Record', 'تسجيل'),
    'Posted': PlainLabel('Recorded', 'مسجّل'),
    'Posting': PlainLabel('Recording', 'التسجيل'),
    'Posted to the ledger': PlainLabel('Recorded in the accounts', 'مسجّل في الحسابات'),
    'Can be posted to': PlainLabel('Can record entries', 'يقبل التسجيل'),
    'Reversal': PlainLabel('Undo entry', 'قيد عكسي'),
    'Reversal posted': PlainLabel('Entry undone', 'تم عكس القيد'),
    'Allocate': PlainLabel('Match to invoices', 'ربط بالفواتير'),
    'Allocations': PlainLabel('Matched invoices', 'الفواتير المرتبطة'),
    'Allocated to bills': PlainLabel('Matched bills', 'الفواتير المرتبطة'),
    'Unallocated': PlainLabel('Not matched yet', 'غير مرتبط بعد'),
    'Settle': PlainLabel('Close out', 'تسوية'),
    'Settle deposit': PlainLabel('Return or keep deposit', 'إرجاع التأمين أو احتجازه'),
    'Reconciliation': PlainLabel('Matching', 'المطابقة'),
    'Reconciliation notes': PlainLabel('Matching notes', 'ملاحظات المطابقة'),
    'Write-off': PlainLabel('Mark as uncollectable', 'اعتباره غير قابل للتحصيل'),
    'Capitalised': PlainLabel('Added to assets', 'أضيف إلى الأصول'),
    'Capitalized': PlainLabel('Added to assets', 'أضيف إلى الأصول'),

    // Amounts.
    'Accruals': PlainLabel('Built up so far', 'المستحق حتى الآن'),
    'Accrued': PlainLabel('Built up so far', 'المستحق حتى الآن'),
    'Gratuity accrued': PlainLabel('End-of-service benefit so far', 'مكافأة نهاية الخدمة حتى الآن'),
    'Net': PlainLabel('After deductions', 'بعد الخصومات'),
    'Net pay': PlainLabel('Take-home pay', 'صافي الراتب المستلم'),
    'Net before tax': PlainLabel('Amount before tax', 'المبلغ قبل الضريبة'),
    'Net to owner': PlainLabel('Owner receives', 'ما يستلمه المالك'),
    'Overhead': PlainLabel('Indirect costs', 'تكاليف غير مباشرة'),
    'Overhead per unit': PlainLabel('Indirect cost per unit', 'التكلفة غير المباشرة لكل وحدة'),
    'Overhead cost per unit': PlainLabel('Indirect cost per unit', 'التكلفة غير المباشرة لكل وحدة'),
    'Variance': PlainLabel('Difference', 'الفرق'),
    'Depreciation': PlainLabel('Loss in value', 'انخفاض القيمة'),
    'VAT': PlainLabel('Sales tax (VAT)', 'ضريبة القيمة المضافة'),

    // Documents / parties.
    'Credit note': PlainLabel('Credit note (refund)', 'إشعار دائن (استرداد)'),
    'Credit notes': PlainLabel('Credit notes (refunds)', 'إشعارات دائنة (استرداد)'),
    'Apply credit': PlainLabel('Use credit balance', 'استخدام الرصيد الدائن'),
    'Statement': PlainLabel('Account summary', 'كشف الحساب'),
    'Customer statement': PlainLabel('Customer account summary', 'كشف حساب العميل'),
    'Owner statement': PlainLabel('Owner account summary', 'كشف حساب المالك'),
    'Vendor': PlainLabel('Supplier', 'مورّد'),
    'Vendors': PlainLabel('Suppliers', 'الموردون'),
    'Vendor bill': PlainLabel('Supplier bill', 'فاتورة المورّد'),
    'Vendor reference': PlainLabel("Supplier's reference", 'مرجع المورّد'),
    'Requisition': PlainLabel('Purchase request', 'طلب شراء'),
    'Requisitions': PlainLabel('Purchase requests', 'طلبات الشراء'),
    'BOM': PlainLabel('Bill of materials (recipe)', 'قائمة المواد (الوصفة)'),
    'BOMs': PlainLabel('Bills of materials (recipes)', 'قوائم المواد (الوصفات)'),
    'Bill of materials': PlainLabel('Bill of materials (recipe)', 'قائمة المواد (الوصفة)'),
  };

  static final Map<String, PlainLabel> _byKey = {
    for (final e in terms.entries) _norm(e.key): e.value,
  };

  static String _norm(String s) => s.trim().toLowerCase();

  /// Plain wording for [jargon] in [languageCode] ('en' / 'ar'), or null when it isn't a known term.
  static String? lookup(String jargon, {String languageCode = 'en'}) => _byKey[_norm(jargon)]?.forLanguage(languageCode);

  /// The [PlainLabel] pair for [jargon], or null.
  static PlainLabel? entry(String jargon) => _byKey[_norm(jargon)];

  /// Plain wording for a caption in the current locale (Arabic when locale is 'ar' or text is RTL); returns [jargon] unchanged when unknown.
  /// Captions only (see the file header).
  static String of(BuildContext context, String jargon) {
    final rtl = Directionality.maybeOf(context) == TextDirection.rtl;
    final code = rtl ? 'ar' : (Localizations.maybeLocaleOf(context)?.languageCode ?? 'en');
    return lookup(jargon, languageCode: code) ?? jargon;
  }

  /// Plain wording or the original caption, without a context.
  static String plain(String jargon, {String languageCode = 'en'}) => lookup(jargon, languageCode: languageCode) ?? jargon;
}

/// `'Receivables ageing'.plain(context)` sugar for captions.
extension PlainLabelString on String {
  String plain(BuildContext context) => PlainLabels.of(context, this);
}
