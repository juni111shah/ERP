import 'package:flutter/painting.dart';

/// The product brand, in one place. Same identity as the web portal (`frontend/src/app/shared/brand-logo.component.ts`,
/// `frontend/public/highersquare-*`): the "HigherSquare" word in brand green, "360" in deep teal, and the emblem of a
/// green rounded square holding a white 360 ring with rising bars and a trend arrow (vector copy in [BrandMark]).
class Brand {
  Brand._();

  /// Full product name, shown on sign-in, splash, the intro tour, the About box and as `MaterialApp.title`.
  static const name = 'HigherSquare 360';

  /// The two halves of [name], styled separately in the wordmark (as on the web).
  static const word = 'HigherSquare';
  static const suffix = '360';

  /// Arabic name used by the web portal (`phrases.ar.ts`). Kept for reference; the mobile wordmark stays Latin.
  static const nameAr = 'هاير سكوير 360';

  /// Home-screen label (Android `android:label`, iOS `CFBundleDisplayName`). Launchers cut labels at about 12
  /// characters, so the full name would show as "HigherSquar…"; the short form keeps the "360".
  static const launcherLabel = 'HS 360';

  static const tagline = 'Enterprise resource planning';
}

/// Brand palette taken from the web logo and emblem (light and dark variants match the web's wordmark colours).
class BrandColors {
  BrandColors._();

  /// Emblem tile and ring green, and the bars/line (sampled from `highersquare-emblem.png`).
  static const tile = Color(0xFF4FA077);
  static const tileDeep = Color(0xFF3A8A62);
  static const teal = Color(0xFF2A8A93);
  static const navy = Color(0xFF1F3A44);

  /// Wordmark: "HigherSquare" / "360" on light and on dark surfaces (web `.name-higher` / `.name-360`).
  static const wordLight = Color(0xFF359867);
  static const suffixLight = Color(0xFF215A67);
  static const wordDark = Color(0xFF4ADE80);
  static const suffixDark = Color(0xFF38BDF8);
}
