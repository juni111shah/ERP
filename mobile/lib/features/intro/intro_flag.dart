import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/session.dart';

/// Store key for the first-open intro tour. Distinct from the post-sign-in coach marks (`onboarding_tour_v1_seen`).
class IntroKeys {
  IntroKeys._();

  static const seen = 'intro_tour_v1_seen';

  /// Route of the tour (`/intro`), kept apart from any other onboarding route.
  static const route = '/intro';
}

/// Whether the first-open intro tour still has to be shown.
enum IntroStatus {
  /// Not read from the store yet; the router holds on the splash screen.
  unknown,

  /// Never finished on this device: show the tour.
  show,

  /// Skipped or finished once; never shown again.
  seen,
}

class IntroController extends Notifier<IntroStatus> {
  @override
  IntroStatus build() => IntroStatus.unknown;

  /// Reads the flag once at app start.
  Future<void> load() async {
    if (state != IntroStatus.unknown) return;
    String? value;
    try {
      value = await ref.read(storeProvider).read(IntroKeys.seen);
    } catch (_) {
      // If the keystore cannot be read, do not trap the user in the tour on every start.
      value = '1';
    }
    if (state == IntroStatus.unknown) state = value == '1' ? IntroStatus.seen : IntroStatus.show;
  }

  /// Skip or Get started: remember it and let the router move on.
  Future<void> markSeen() async {
    state = IntroStatus.seen;
    try {
      await ref.read(storeProvider).write(IntroKeys.seen, '1');
    } catch (_) {
      // Moving on matters more than the flag; at worst the tour shows once more.
    }
  }
}

final introProvider = NotifierProvider<IntroController, IntroStatus>(IntroController.new);
