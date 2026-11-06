import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

/// The languages the app can resolve a device's language to. English comes first, so any other language falls back to it.
const appLocales = [Locale('en'), Locale('ar')];

const appLocalizationsDelegates = <LocalizationsDelegate<dynamic>>[
  GlobalMaterialLocalizations.delegate,
  GlobalWidgetsLocalizations.delegate,
  GlobalCupertinoLocalizations.delegate,
];

/// The device's language as the app resolved it. A screen that is translated reads this and switches itself over (see `AssistantScope`).
class AppLanguage extends InheritedWidget {
  const AppLanguage({super.key, required this.locale, required super.child});

  final Locale locale;

  static Locale? maybeOf(BuildContext context) => context.dependOnInheritedWidgetOfExactType<AppLanguage>()?.locale;

  @override
  bool updateShouldNotify(AppLanguage oldWidget) => oldWidget.locale != locale;
}

/// `MaterialApp.builder`. Only the assistant is translated so far, so every other screen stays English and left to right on an Arabic
/// device: mirrored English moves punctuation to the wrong end of a line, and many screens still use fixed left and right insets.
Widget appLanguageBuilder(BuildContext context, Widget? child) => AppLanguage(
      locale: Localizations.localeOf(context),
      child: Localizations.override(context: context, locale: appLocales.first, child: child),
    );
