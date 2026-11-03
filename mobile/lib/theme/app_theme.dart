import 'package:flutter/material.dart';

import 'tokens.dart';

/// Neutrals and brand values that Material's ColorScheme has no slot for. Read with `ErpTheme.of(context)`.
@immutable
class ErpTheme extends ThemeExtension<ErpTheme> {
  const ErpTheme({
    required this.brand,
    required this.brandHover,
    required this.brandSubtle,
    required this.canvas,
    required this.surface,
    required this.sunken,
    required this.borderSubtle,
    required this.border,
    required this.text,
    required this.text2,
    required this.muted,
    required this.heroGradient,
  });

  final Color brand;
  final Color brandHover;
  final Color brandSubtle;
  final Color canvas;
  final Color surface;
  final Color sunken;
  final Color borderSubtle;
  final Color border;
  final Color text;
  final Color text2;
  final Color muted;

  /// Brand hero (sign-in header, detail header).
  final Gradient heroGradient;

  static const light = ErpTheme(
    brand: ErpColors.brand,
    brandHover: ErpColors.brandHover,
    brandSubtle: ErpColors.brandSubtle,
    canvas: ErpColors.canvas,
    surface: ErpColors.surface,
    sunken: ErpColors.sunken,
    borderSubtle: ErpColors.borderSubtle,
    border: ErpColors.border,
    text: ErpColors.text,
    text2: ErpColors.text2,
    muted: ErpColors.muted,
    heroGradient: LinearGradient(begin: AlignmentDirectional.topStart, end: AlignmentDirectional.bottomEnd, colors: [ErpColors.brandInk, ErpColors.brandHover]),
  );

  static const dark = ErpTheme(
    brand: ErpDark.brand,
    brandHover: ErpDark.brand,
    brandSubtle: ErpDark.brandSubtle,
    canvas: ErpDark.canvas,
    surface: ErpDark.surface,
    sunken: ErpDark.sunken,
    borderSubtle: ErpDark.borderSubtle,
    border: ErpDark.border,
    text: ErpDark.text,
    text2: ErpDark.text2,
    muted: ErpDark.muted,
    heroGradient: LinearGradient(begin: AlignmentDirectional.topStart, end: AlignmentDirectional.bottomEnd, colors: [Color(0xFF06331F), Color(0xFF0A623D)]),
  );

  static ErpTheme of(BuildContext context) => Theme.of(context).extension<ErpTheme>() ?? light;

  @override
  ErpTheme copyWith({Color? brand, Color? canvas, Color? surface}) => ErpTheme(
        brand: brand ?? this.brand,
        brandHover: brandHover,
        brandSubtle: brandSubtle,
        canvas: canvas ?? this.canvas,
        surface: surface ?? this.surface,
        sunken: sunken,
        borderSubtle: borderSubtle,
        border: border,
        text: text,
        text2: text2,
        muted: muted,
        heroGradient: heroGradient,
      );

  @override
  ErpTheme lerp(ThemeExtension<ErpTheme>? other, double t) {
    if (other is! ErpTheme) return this;
    Color l(Color a, Color b) => Color.lerp(a, b, t)!;
    return ErpTheme(
      brand: l(brand, other.brand),
      brandHover: l(brandHover, other.brandHover),
      brandSubtle: l(brandSubtle, other.brandSubtle),
      canvas: l(canvas, other.canvas),
      surface: l(surface, other.surface),
      sunken: l(sunken, other.sunken),
      borderSubtle: l(borderSubtle, other.borderSubtle),
      border: l(border, other.border),
      text: l(text, other.text),
      text2: l(text2, other.text2),
      muted: l(muted, other.muted),
      heroGradient: t < 0.5 ? heroGradient : other.heroGradient,
    );
  }
}

/// The app's Material 3 theme built from the web portal tokens.
class AppTheme {
  AppTheme._();

  static ThemeData light() => _build(Brightness.light, ErpTheme.light);
  static ThemeData dark() => _build(Brightness.dark, ErpTheme.dark);

  static ThemeData _build(Brightness brightness, ErpTheme t) {
    final isDark = brightness == Brightness.dark;
    final scheme = ColorScheme.fromSeed(
      seedColor: ErpColors.brand,
      brightness: brightness,
      primary: t.brand,
      onPrimary: isDark ? const Color(0xFF00210F) : Colors.white,
      primaryContainer: isDark ? const Color(0xFF12362A) : ErpColors.brandSubtle,
      onPrimaryContainer: isDark ? const Color(0xFFB6F0D3) : ErpColors.brandInk,
      secondary: isDark ? const Color(0xFF7CC9A6) : ErpColors.brandHover,
      surface: t.surface,
      onSurface: t.text,
      onSurfaceVariant: t.muted,
      surfaceContainerLowest: t.surface,
      surfaceContainerLow: t.surface,
      surfaceContainer: isDark ? const Color(0xFF1B1E23) : ErpColors.subtle,
      surfaceContainerHigh: t.sunken,
      surfaceContainerHighest: t.sunken,
      outline: t.border,
      outlineVariant: t.borderSubtle,
      error: isDark ? const Color(0xFFFF7875) : ErpColors.danger,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      fontFamily: ErpFonts.family,
      fontFamilyFallback: ErpFonts.fallback,
      scaffoldBackgroundColor: t.canvas,
      canvasColor: t.canvas,
      dividerColor: t.borderSubtle,
      extensions: [t],
    );

    final text = base.textTheme.apply(bodyColor: t.text, displayColor: t.text).copyWith(
          displaySmall: ErpType.displayStyle.copyWith(color: t.text),
          headlineSmall: base.textTheme.headlineSmall?.copyWith(fontSize: ErpType.headline, fontWeight: FontWeight.w700, letterSpacing: -0.2, color: t.text),
          titleLarge: base.textTheme.titleLarge?.copyWith(fontSize: ErpType.heading, fontWeight: FontWeight.w700, letterSpacing: -0.1, color: t.text),
          titleMedium: base.textTheme.titleMedium?.copyWith(fontSize: ErpType.title, fontWeight: FontWeight.w700, color: t.text),
          titleSmall: base.textTheme.titleSmall?.copyWith(fontSize: ErpType.body, fontWeight: FontWeight.w600, color: t.text),
          bodyLarge: base.textTheme.bodyLarge?.copyWith(fontSize: ErpType.bodyLarge, color: t.text),
          bodyMedium: base.textTheme.bodyMedium?.copyWith(fontSize: ErpType.body, color: t.text2),
          bodySmall: base.textTheme.bodySmall?.copyWith(fontSize: ErpType.caption, color: t.muted),
          labelLarge: base.textTheme.labelLarge?.copyWith(fontSize: ErpType.body, fontWeight: FontWeight.w600),
          labelMedium: base.textTheme.labelMedium?.copyWith(fontSize: ErpType.caption, fontWeight: FontWeight.w600, color: t.muted),
          labelSmall: base.textTheme.labelSmall?.copyWith(fontSize: ErpType.label, fontWeight: FontWeight.w600, letterSpacing: 0.5, color: t.muted),
        );

    OutlineInputBorder border(Color c, [double w = 1]) => OutlineInputBorder(borderRadius: BorderRadius.circular(ErpRadius.input), borderSide: BorderSide(color: c, width: w));
    const buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(ErpRadius.card)));
    const buttonSize = Size(64, 46);
    const buttonText = TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.body, fontWeight: FontWeight.w600);

    return base.copyWith(
      textTheme: text,
      appBarTheme: AppBarTheme(
        backgroundColor: t.canvas,
        surfaceTintColor: Colors.transparent,
        foregroundColor: t.text,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        centerTitle: false,
        titleTextStyle: text.titleLarge,
      ),
      cardTheme: CardThemeData(
        color: t.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: const EdgeInsets.only(bottom: ErpSpace.s2),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.card), side: BorderSide(color: t.borderSubtle)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: t.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: ErpSpace.s35, vertical: ErpSpace.s35),
        border: border(t.border),
        enabledBorder: border(t.border),
        focusedBorder: border(t.brand, 1.5),
        errorBorder: border(scheme.error),
        focusedErrorBorder: border(scheme.error, 1.5),
        prefixIconColor: t.muted,
        suffixIconColor: t.muted,
        labelStyle: TextStyle(color: t.muted),
        hintStyle: TextStyle(color: t.muted),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(minimumSize: buttonSize, shape: buttonShape, textStyle: buttonText, backgroundColor: t.brand, foregroundColor: scheme.onPrimary),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(minimumSize: buttonSize, shape: buttonShape, textStyle: buttonText, elevation: 0, backgroundColor: t.surface, foregroundColor: t.text),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(minimumSize: buttonSize, shape: buttonShape, textStyle: buttonText, foregroundColor: t.text, side: BorderSide(color: t.border), backgroundColor: t.surface),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: t.brandHover, textStyle: buttonText.copyWith(fontSize: ErpType.bodySmall)),
      ),
      iconButtonTheme: IconButtonThemeData(style: IconButton.styleFrom(foregroundColor: t.text2)),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: t.brand,
        foregroundColor: scheme.onPrimary,
        elevation: 4,
        highlightElevation: 6,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        extendedTextStyle: buttonText.copyWith(fontSize: ErpType.body),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: t.surface,
        selectedColor: t.brandSubtle,
        side: BorderSide(color: t.border),
        shape: const StadiumBorder(),
        labelStyle: TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.bodySmall, fontWeight: FontWeight.w500, color: t.text2),
        secondaryLabelStyle: TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.bodySmall, fontWeight: FontWeight.w600, color: t.brand),
        checkmarkColor: t.brand,
        showCheckmark: false,
        padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s1),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: t.surface.withValues(alpha: 0.96),
        surfaceTintColor: Colors.transparent,
        indicatorColor: t.brandSubtle,
        indicatorShape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
        height: 68,
        elevation: 0,
        labelTextStyle: WidgetStateProperty.resolveWith((s) => TextStyle(
              fontFamily: ErpFonts.family,
              fontSize: ErpType.label,
              fontWeight: s.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
              color: s.contains(WidgetState.selected) ? t.brandHover : t.muted,
            )),
        iconTheme: WidgetStateProperty.resolveWith((s) => IconThemeData(size: 22, color: s.contains(WidgetState.selected) ? t.brandHover : t.muted)),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: t.brandHover,
        unselectedLabelColor: t.muted,
        indicatorColor: t.brand,
        dividerColor: t.border,
        labelStyle: const TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.bodySmall, fontWeight: FontWeight.w700),
        unselectedLabelStyle: const TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.bodySmall, fontWeight: FontWeight.w500),
      ),
      listTileTheme: ListTileThemeData(
        iconColor: t.text2,
        titleTextStyle: TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.body, fontWeight: FontWeight.w600, color: t.text),
        subtitleTextStyle: TextStyle(fontFamily: ErpFonts.family, fontSize: ErpType.caption, color: t.muted),
      ),
      dividerTheme: DividerThemeData(color: t.borderSubtle, thickness: 1, space: 1),
      switchTheme: SwitchThemeData(
        trackColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? t.brand : t.sunken),
        thumbColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? Colors.white : t.muted),
        trackOutlineColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? t.brand : t.border),
      ),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? t.brand : Colors.transparent),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
      ),
      snackBarTheme: SnackBarThemeData(behavior: SnackBarBehavior.floating, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.card))),
      dialogTheme: DialogThemeData(backgroundColor: t.surface, surfaceTintColor: Colors.transparent, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.panel))),
      bottomSheetTheme: BottomSheetThemeData(backgroundColor: t.surface, surfaceTintColor: Colors.transparent, showDragHandle: true),
      searchBarTheme: SearchBarThemeData(
        elevation: const WidgetStatePropertyAll(0),
        backgroundColor: WidgetStatePropertyAll(t.surface),
        side: WidgetStatePropertyAll(BorderSide(color: t.border)),
        shape: WidgetStatePropertyAll(RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.input))),
        constraints: const BoxConstraints(minHeight: 44),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: t.brand, linearTrackColor: t.sunken),
    );
  }
}
