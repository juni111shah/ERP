import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'session.dart';

/// Storage key for the chosen appearance ("light", "dark" or "system").
const themeModeKey = 'theme_mode';

/// The app's appearance: Light, Dark or follow the system. Kept in the existing key-value store so it survives restarts.
class ThemeModeController extends Notifier<ThemeMode> {
  bool _chosen = false;

  @override
  ThemeMode build() {
    Future.microtask(_restore);
    return ThemeMode.system;
  }

  Future<void> _restore() async {
    try {
      final saved = await ref.read(storeProvider).read(themeModeKey);
      if (!ref.mounted || _chosen) return;
      final mode = parseThemeMode(saved);
      if (mode != null) state = mode;
    } catch (_) {
      // No stored choice, or storage is unavailable: keep following the system.
    }
  }

  Future<void> set(ThemeMode mode) async {
    _chosen = true;
    state = mode;
    try {
      await ref.read(storeProvider).write(themeModeKey, mode.name);
    } catch (_) {
      // Still applied for this run.
    }
  }
}

ThemeMode? parseThemeMode(String? value) => switch (value) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      'system' => ThemeMode.system,
      _ => null,
    };

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);
