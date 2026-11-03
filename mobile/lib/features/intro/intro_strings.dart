import 'package:flutter/widgets.dart';

/// Copy for the first-open intro tour, in English and Arabic. Arabic is picked by an `ar` locale or a right-to-left
/// direction (the app declares no supported locales yet). Value-led copy: outcomes, no figures or claims we can't back.
class IntroStrings {
  const IntroStrings._({
    required this.eyebrows,
    required this.titles,
    required this.bodies,
    required this.featuresTitle,
    required this.features,
    required this.skip,
    required this.next,
    required this.getStarted,
    required this.back,
    required this.previousScreen,
    required String Function(int n, int total) step,
    required this.illustrationLabel,
  }) : _step = step; // ignore: prefer_initializing_formals (a named parameter can't be a private initialising formal)

  /// Small brand-coloured label above each title.
  final List<String> eyebrows;
  final List<String> titles;
  final List<String> bodies;

  /// "Everything in one app" chip panel on the last screen; every chip is a feature that ships today.
  final String featuresTitle;
  final List<String> features;

  final String skip;
  final String next;
  final String getStarted;

  /// Back control on screens 2-5: visible label for semantics, plus a hint read after it.
  final String back;
  final String previousScreen;
  final String Function(int n, int total) _step;
  final String illustrationLabel;

  /// "2 of 5".
  String step(int n, int total) => _step(n, total);

  static const en = IntroStrings._(
    eyebrows: ['Overview', 'Cash flow', 'Invoicing', 'Alerts and settings', 'All in one'],
    titles: [
      'Know where your business stands in seconds',
      'See billing turn into cash',
      'Get paid faster',
      'Stay on top of what needs you',
      'Run your whole business from one app',
    ],
    bodies: [
      'Sales, cash and money owed to you, ready on one screen when you open the app.',
      'Compare what you billed with what you collected, month by month, and act before gaps grow.',
      'Send invoices and record receipts from your phone, wherever you are.',
      'The bell shows what needs your attention. Pick light or dark to suit you.',
      'Finance, sales, stock, people and property together. Search any list to find a record fast.',
    ],
    featuresTitle: 'Everything in one app',
    features: [
      'Invoices & receipts',
      'Bills & payments',
      'Journals',
      'Chart of accounts',
      'Approvals',
      'Inventory',
      'HR & payroll',
      'Property',
      'Sales & CRM',
      'Print & share',
      'Country tax packs',
      'Light & dark',
    ],
    skip: 'Skip',
    next: 'Next',
    getStarted: 'Get started',
    back: 'Back',
    previousScreen: 'Previous screen',
    step: _enStep,
    illustrationLabel: 'Illustration of the app',
  );

  // Arabic: flagged for native review in the PR.
  static const ar = IntroStrings._(
    eyebrows: ['نظرة عامة', 'التدفق النقدي', 'الفوترة', 'التنبيهات والإعدادات', 'كل شيء في مكان واحد'],
    titles: [
      'اعرف وضع أعمالك في ثوانٍ',
      'شاهد فواتيرك تتحوّل إلى نقد',
      'احصل على مستحقاتك أسرع',
      'ابقَ على اطلاع بما يحتاجك',
      'أدِر أعمالك كلها من تطبيق واحد',
    ],
    bodies: [
      'المبيعات والنقد والمبالغ المستحقة لك، جاهزة في شاشة واحدة فور فتح التطبيق.',
      'قارن ما فوترته بما حصّلته شهرًا بشهر، وتصرّف قبل أن تتّسع الفجوة.',
      'أرسل الفواتير وسجّل الإيصالات من هاتفك أينما كنت.',
      'يعرض الجرس ما يحتاج إلى انتباهك. واختر الوضع الفاتح أو الداكن كما يناسبك.',
      'المالية والمبيعات والمخزون والموظفون والعقارات معًا. ابحث في أي قائمة لتصل إلى السجل بسرعة.',
    ],
    featuresTitle: 'كل شيء في تطبيق واحد',
    features: [
      'الفواتير والإيصالات',
      'فواتير الموردين والمدفوعات',
      'القيود اليومية',
      'دليل الحسابات',
      'الموافقات',
      'المخزون',
      'الموارد البشرية والرواتب',
      'العقارات',
      'المبيعات وإدارة العملاء',
      'الطباعة والمشاركة',
      'حزم ضريبية حسب الدولة',
      'الوضع الفاتح والداكن',
    ],
    skip: 'تخطي',
    next: 'التالي',
    getStarted: 'ابدأ الآن',
    back: 'رجوع',
    previousScreen: 'الشاشة السابقة',
    step: _arStep,
    illustrationLabel: 'رسم توضيحي للتطبيق',
  );

  static String _enStep(int n, int total) => '$n of $total';
  static String _arStep(int n, int total) => '$n من $total';

  static IntroStrings of(BuildContext context) {
    final lang = Localizations.maybeLocaleOf(context)?.languageCode;
    final rtl = Directionality.maybeOf(context) == TextDirection.rtl;
    return lang == 'ar' || rtl ? ar : en;
  }
}
