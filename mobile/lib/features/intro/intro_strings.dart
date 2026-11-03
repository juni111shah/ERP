import 'package:flutter/widgets.dart';

/// Copy for the first-open intro tour, in English and Arabic. Arabic is picked by an `ar` locale or a right-to-left
/// direction (the app declares no supported locales yet).
class IntroStrings {
  const IntroStrings._(this.titles, this.bodies, this.skip, this.next, this.getStarted, this._step, this.illustrationLabel);

  final List<String> titles;
  final List<String> bodies;
  final String skip;
  final String next;
  final String getStarted;
  final String Function(int n, int total) _step;
  final String illustrationLabel;

  /// "2 of 5".
  String step(int n, int total) => _step(n, total);

  static const en = IntroStrings._(
    [
      'Your key numbers at a glance',
      'See money in and money out',
      'Invoices and receipts on the go',
      'Make the app yours',
      'Jump anywhere in one tap',
    ],
    [
      'Four highlight cards sit at the top. Tap a category to switch them, or swipe to see more.',
      'Compare what you billed with what you collected, month by month.',
      'See who has paid and who is late, and record a payment wherever you are.',
      'Tap your picture for settings and light or dark mode. The bell shows what needs you.',
      'Search for a customer, an invoice or a screen and go straight there.',
    ],
    'Skip',
    'Next',
    'Get started',
    _enStep,
    'Illustration of the app',
  );

  static const ar = IntroStrings._(
    [
      'أهم أرقامك في لمحة',
      'تابع الأموال الواردة والصادرة',
      'الفواتير والإيصالات أينما كنت',
      'اجعل التطبيق على ذوقك',
      'انتقل إلى أي مكان بلمسة',
    ],
    [
      'تظهر أربع بطاقات رئيسية في الأعلى. اضغط على فئة للتبديل بينها، أو اسحب لرؤية المزيد.',
      'قارن ما أصدرت به الفواتير بما حصّلته، شهرًا بشهر.',
      'اعرف من دفع ومن تأخر، وسجّل دفعة أينما كنت.',
      'اضغط على صورتك للإعدادات والوضع الفاتح أو الداكن. يُظهر الجرس ما يحتاج إلى انتباهك.',
      'ابحث عن عميل أو فاتورة أو شاشة وانتقل إليها مباشرة.',
    ],
    'تخطي',
    'التالي',
    'ابدأ الآن',
    _arStep,
    'رسم توضيحي للتطبيق',
  );

  static String _enStep(int n, int total) => '$n of $total';
  static String _arStep(int n, int total) => '$n من $total';

  static IntroStrings of(BuildContext context) {
    final lang = Localizations.maybeLocaleOf(context)?.languageCode;
    final rtl = Directionality.maybeOf(context) == TextDirection.rtl;
    return lang == 'ar' || rtl ? ar : en;
  }
}
