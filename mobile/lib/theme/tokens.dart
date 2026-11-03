import 'package:flutter/material.dart';

/// Design tokens shared with the web portal (frontend/src/styles/tokens.scss, _stat-card.scss, dashboard.page.ts).
/// Light values; dark equivalents are in [ErpDark]. Widgets should prefer `ErpTheme.of(context)` / `Theme.of(context)` so dark
/// mode works, and use these constants only where a fixed brand colour is meant.
class ErpColors {
  ErpColors._();

  static const brand = Color(0xFF10955D);
  static const brandHover = Color(0xFF0D7B4C);
  static const brandActive = Color(0xFF0A623D);
  static const brandSubtle = Color(0xFFE6F7EF);
  static const brand100 = Color(0xFFC8EEDC);
  static const brand300 = Color(0xFF66CBA2);
  static const brandInk = Color(0xFF074A2E);

  static const canvas = Color(0xFFF4F6F8);
  static const surface = Color(0xFFFFFFFF);
  static const subtle = Color(0xFFFAFBFC);
  static const sunken = Color(0xFFEEF0F3);
  static const borderSubtle = Color(0xFFEEF0F3);
  static const border = Color(0xFFE2E5EA);

  static const text = Color(0xFF1F2329);
  static const text2 = Color(0xFF434A55);
  static const muted = Color(0xFF626977);
  static const disabled = Color(0xFFA3AAB5);

  static const billed = Color(0xFF2A78D6);
  static const collected = Color(0xFFEB6834);

  static const danger = Color(0xFFCF1322);
}

/// Dark-mode equivalents of the neutrals and brand.
class ErpDark {
  ErpDark._();

  static const brand = Color(0xFF34BC86);
  static const brandSubtle = Color(0xFF12362A);
  static const canvas = Color(0xFF0D0F12);
  static const surface = Color(0xFF16181C);
  static const sunken = Color(0xFF1F2227);
  static const borderSubtle = Color(0xFF24272D);
  static const border = Color(0xFF2E323A);
  static const text = Color(0xFFE8EAED);
  static const text2 = Color(0xFFC3C7CE);
  static const muted = Color(0xFF9097A3);
}

/// Ant-style status tones (tag, alert, banner).
enum ErpTone { success, warning, error, info, neutral }

extension ErpToneColors on ErpTone {
  Color get bg => switch (this) {
        ErpTone.success => const Color(0xFFF6FFED),
        ErpTone.warning => const Color(0xFFFFFBE6),
        ErpTone.error => const Color(0xFFFFF2F0),
        ErpTone.info => const Color(0xFFE6F4FF),
        ErpTone.neutral => const Color(0xFFEEF2F6),
      };

  Color get fg => switch (this) {
        ErpTone.success => const Color(0xFF237804),
        ErpTone.warning => const Color(0xFF874D00),
        ErpTone.error => const Color(0xFFCF1322),
        ErpTone.info => const Color(0xFF0958D9),
        ErpTone.neutral => const Color(0xFF3D4D5C),
      };

  Color get border => switch (this) {
        ErpTone.success => const Color(0xFFB7EB8F),
        ErpTone.warning => const Color(0xFFFFE58F),
        ErpTone.error => const Color(0xFFFFCCC7),
        ErpTone.info => const Color(0xFF91CAFF),
        ErpTone.neutral => const Color(0xFFCBD5E0),
      };

  /// Dark mode: tinted translucent background, lighter text.
  Color bgFor(Brightness b) => b == Brightness.dark ? fg.withValues(alpha: 0.18) : bg;
  Color fgFor(Brightness b) => b == Brightness.dark ? Color.lerp(fg, Colors.white, 0.45)! : fg;
  Color borderFor(Brightness b) => b == Brightness.dark ? fg.withValues(alpha: 0.35) : border;

  /// The tone of a document status used by invoices, bills, receipts, orders...
  static ErpTone forStatus(String? status) => switch ((status ?? '').toLowerCase()) {
        'paid' || 'posted' || 'approved' || 'active' || 'completed' || 'done' || 'received' || 'closed' => ErpTone.success,
        'partlypaid' || 'pending' || 'submitted' || 'onhold' || 'partiallyreceived' || 'inprogress' => ErpTone.warning,
        'voided' || 'rejected' || 'cancelled' || 'canceled' || 'overdue' || 'failed' => ErpTone.error,
        'issued' || 'sent' || 'open' || 'confirmed' || 'new' => ErpTone.info,
        _ => ErpTone.neutral,
      };
}

/// Stat-card stripe / value colour pairs (web `_stat-card.scss`).
class ErpStripe {
  const ErpStripe(this.stripe, this.value);

  final Color stripe;
  final Color value;

  static const green = ErpStripe(Color(0xFF10955D), Color(0xFF0D7B4C));
  static const blue = ErpStripe(Color(0xFF2563EB), Color(0xFF1D4ED8));
  static const cyan = ErpStripe(Color(0xFF0EA5E9), Color(0xFF0284C7));
  static const amber = ErpStripe(Color(0xFFF59E0B), Color(0xFFD97706));
  static const purple = ErpStripe(Color(0xFF8B5CF6), Color(0xFF7C3AED));

  static const all = [green, blue, cyan, amber, purple];
}

class ErpRadius {
  ErpRadius._();

  static const double control = 6;
  static const double input = 10;
  static const double card = 12;
  static const double panel = 14;
  static const double pill = 999;
}

/// 4px spacing grid.
class ErpSpace {
  ErpSpace._();

  static const double s1 = 4;
  static const double s2 = 8;
  static const double s3 = 12;
  static const double s4 = 16;
  static const double s5 = 20;
  static const double s6 = 24;
  static const double s8 = 32;
  static const double s10 = 40;
  static const double s12 = 48;

  // Half steps, for hairline gaps and compact pills (2, 6, 10, 14).
  static const double s05 = 2;
  static const double s15 = 6;
  static const double s25 = 10;
  static const double s35 = 14;
}

/// The one type scale. Sizes are named by role; [ThemeData.textTheme] is built from them (see `AppTheme`), and
/// widgets that need a raw size use these constants instead of literals.
///
/// | role          | size | textTheme slot              |
/// |---------------|------|-----------------------------|
/// | display       | 24   | displaySmall (KPI values)   |
/// | headline      | 22   | headlineSmall               |
/// | heading       | 20   | titleLarge (app bar)        |
/// | titleLarge    | 18   | sheet / section titles      |
/// | title         | 16   | titleMedium                 |
/// | bodyLarge     | 15   | bodyLarge                   |
/// | body          | 14   | bodyMedium                  |
/// | bodySmall     | 13   | secondary lines             |
/// | caption       | 12   | bodySmall                   |
/// | label         | 11   | labelSmall (caps sections)  |
/// | micro         | 10   | badges, axis ticks          |
class ErpType {
  ErpType._();

  static const double micro = 10;
  static const double label = 11;
  static const double caption = 12;
  static const double bodySmall = 13;
  static const double body = 14;
  static const double bodyLarge = 15;
  static const double title = 16;
  static const double titleLarge = 18;
  static const double heading = 20;
  static const double headline = 22;
  static const double display = 24;

  /// Big tabular figure (KPI values, hero amounts).
  static const displayStyle = TextStyle(fontSize: display, fontWeight: FontWeight.w700, letterSpacing: -0.48, height: 1.2, fontFeatures: ErpFonts.tabular);

  /// Card / sheet / state titles.
  static const titleStyle = TextStyle(fontSize: title, fontWeight: FontWeight.w700);

  /// Small caps section label ("HIGHLIGHTS"); upper-case the text yourself (not in Arabic).
  static const sectionLabel = TextStyle(fontSize: label, fontWeight: FontWeight.w600, letterSpacing: 0.55);

  static const bodyStyle = TextStyle(fontSize: body);
  static const captionStyle = TextStyle(fontSize: caption);

  /// Tabular figures so amounts line up.
  static const numbers = TextStyle(fontFeatures: ErpFonts.tabular);
}

/// Soft card shadow (web `--shc`) and raised panel shadow (`--sh3`).
class ErpShadow {
  ErpShadow._();

  static const card = [
    BoxShadow(color: Color(0x05000000), blurRadius: 3, offset: Offset(0, 1)),
    BoxShadow(color: Color(0x05000000), blurRadius: 12, offset: Offset(0, 4)),
  ];
  static const raised = [
    BoxShadow(color: Color(0x1A101828), blurRadius: 24, spreadRadius: -4, offset: Offset(0, 12)),
    BoxShadow(color: Color(0x0F101828), blurRadius: 8, spreadRadius: -4, offset: Offset(0, 4)),
  ];
}

/// Font families bundled under mobile/assets/fonts (both SIL OFL 1.1).
class ErpFonts {
  ErpFonts._();

  static const family = 'Inter';
  static const fallback = ['NotoSansArabic'];

  /// Tabular figures so amounts line up in lists.
  static const tabular = [FontFeature.tabularFigures()];
}
