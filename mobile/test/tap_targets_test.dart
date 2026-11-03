// Tap-target audit: every mobile screen must meet Android's 48x48dp tap-target guideline and give every tappable
// thing a label, in left-to-right and right-to-left (Arabic) layouts, on a 360x780dp phone.
//
// No network: the API, session, user, menu, Overview and notifications are faked (see fakes/).
// Each check also appends a line to build/tap_targets_report.txt so failures can be read per screen.

@Timeout(Duration(seconds: 90))
library;

import 'dart:io';

import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:erp_mobile/features/auth/workspace_page.dart';
import 'package:erp_mobile/features/common/paged_list_view.dart';
import 'package:erp_mobile/features/home/home_page.dart';
import 'package:erp_mobile/features/more/more_page.dart';
import 'package:erp_mobile/features/notifications/notifications.dart';
import 'package:erp_mobile/features/onboarding/onboarding_tour.dart';
import 'package:erp_mobile/features/settings/settings_page.dart';
import 'package:erp_mobile/kit/bottom_nav.dart';
import 'package:erp_mobile/kit/expand_search.dart';
import 'package:erp_mobile/kit/list_card.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/screens/all_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fakes/fake_session.dart';

const _report = 'build/tap_targets_report.txt';

const _directions = {'ltr': TextDirection.ltr, 'rtl': TextDirection.rtl};

void _log(String line) {
  final f = File(_report);
  f.parent.createSync(recursive: true);
  f.writeAsStringSync('$line\n', mode: FileMode.append);
}

/// A 360x780dp phone at 3x.
void _phone(WidgetTester t) {
  t.view.physicalSize = const Size(1080, 2340);
  t.view.devicePixelRatio = 3;
  addTearDown(t.view.reset);
}

/// Lets futures resolve and animations finish without waiting on spinners forever.
Future<void> _settle(WidgetTester t, [int frames = 15]) async {
  for (var i = 0; i < frames; i++) {
    await t.pump(const Duration(milliseconds: 100));
  }
}

/// Collects layout errors (overflows) so they are reported instead of hiding the tap-target result.
class _Errors {
  _Errors() : _previous = FlutterError.onError {
    FlutterError.onError = (d) => messages.add(d.exceptionAsString().split('\n').first);
  }

  final void Function(FlutterErrorDetails)? _previous;
  final List<String> messages = [];
  bool _restored = false;

  bool get isNotEmpty => messages.isNotEmpty;

  /// Must run before any expect, or the test binding cannot report the failure.
  void restore() {
    if (_restored) return;
    _restored = true;
    FlutterError.onError = _previous;
  }
}

_Errors _catchErrors() {
  final errors = _Errors();
  addTearDown(errors.restore);
  return errors;
}

/// Logs both guidelines for [where], then asserts them.
Future<void> _check(WidgetTester t, String where, _Errors errors) async {
  final size = await androidTapTargetGuideline.evaluate(t);
  final label = await labeledTapTargetGuideline.evaluate(t);
  String flat(String? s) => (s ?? '').replaceAll('\n', ' ⏎ ');
  _log('$where | size | ${size.passed ? 'PASS' : 'FAIL'} | ${size.passed ? '' : flat(size.reason)}');
  _log('$where | label | ${label.passed ? 'PASS' : 'FAIL'} | ${label.passed ? '' : flat(label.reason)}');
  errors.restore();
  if (errors.isNotEmpty) _log('$where | errors | ${errors.messages.toSet().join(' ; ')}');
  await expectLater(t, meetsGuideline(androidTapTargetGuideline));
  await expectLater(t, meetsGuideline(labeledTapTargetGuideline));
}

Future<void> _open(WidgetTester t, Widget page, TextDirection dir) async {
  _phone(t);
  await t.pumpWidget(fakeHost(page, direction: dir));
  await _settle(t);
}

/// The first list row on screen, if the screen has one.
Finder _firstRow() {
  for (final f in [find.byType(ListItemCard), find.byType(ErpListCard)]) {
    if (f.evaluate().isNotEmpty) return f.first;
  }
  return find.byType(ListTile).evaluate().isNotEmpty ? find.byType(ListTile).first : find.byWidgetPredicate((_) => false);
}

void main() {
  setUpAll(() {
    final f = File(_report);
    if (f.existsSync()) f.deleteSync();
  });

  for (final entry in _directions.entries) {
    final dir = entry.value;
    final d = entry.key;

    group('[$d] app screens', () {
      for (final screen in allScreens) {
        testWidgets('${screen.id} list', (t) async {
          final handle = t.ensureSemantics();
          final errors = _catchErrors();
          await _open(t, Builder(builder: screen.builder), dir);
          await _check(t, '$d | ${screen.id} | list', errors);
          handle.dispose();
        });

        testWidgets('${screen.id} detail', (t) async {
          final handle = t.ensureSemantics();
          final errors = _catchErrors();
          await _open(t, Builder(builder: screen.builder), dir);
          final row = _firstRow();
          if (row.evaluate().isEmpty) {
            errors.restore();
            _log('$d | ${screen.id} | detail | N/A | no list row');
            handle.dispose();
            return;
          }
          await t.tap(row, warnIfMissed: false);
          await _settle(t);
          await _check(t, '$d | ${screen.id} | detail', errors);
          handle.dispose();
        });

        testWidgets('${screen.id} search open', (t) async {
          final handle = t.ensureSemantics();
          final errors = _catchErrors();
          await _open(t, Builder(builder: screen.builder), dir);
          final open = find.descendant(of: find.byType(ExpandSearch), matching: find.byType(IconButton));
          if (open.evaluate().isEmpty) {
            errors.restore();
            _log('$d | ${screen.id} | search | N/A | no search');
            handle.dispose();
            return;
          }
          await t.tap(open.first);
          await _settle(t, 5);
          await t.enterText(find.descendant(of: find.byType(ExpandSearch), matching: find.byType(TextField)), 'acme');
          await _settle(t, 8);
          await _check(t, '$d | ${screen.id} | search', errors);
          handle.dispose();
        });

        testWidgets('${screen.id} new', (t) async {
          final handle = t.ensureSemantics();
          final errors = _catchErrors();
          await _open(t, Builder(builder: screen.builder), dir);
          final fab = find.byType(FloatingActionButton);
          if (fab.evaluate().isEmpty) {
            errors.restore();
            _log('$d | ${screen.id} | new | N/A | no New button');
            handle.dispose();
            return;
          }
          await t.tap(fab.first);
          await _settle(t);
          await _check(t, '$d | ${screen.id} | new', errors);
          handle.dispose();
        });
      }
    });

    group('[$d] shell and account', () {
      for (final tab in const ['Overview', 'Invoices', 'Receipts', 'Customers', 'More']) {
        testWidgets('home shell: $tab tab', (t) async {
          final handle = t.ensureSemantics();
          final errors = _catchErrors();
          await _open(t, const HomePage(), dir);
          final label = find.descendant(of: find.byType(ErpBottomNav), matching: find.text(tab));
          if (tab != 'Overview' && label.evaluate().isNotEmpty) {
            await t.tap(label.first);
            await _settle(t);
          }
          await _check(t, '$d | shell:$tab | page', errors);
          handle.dispose();
        });
      }

      testWidgets('more page', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const Scaffold(body: MorePage()), dir);
        await _check(t, '$d | more | page', errors);
        handle.dispose();
      });

      testWidgets('profile sheet', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const HomePage(), dir);
        await t.tap(find.byKey(const ValueKey('header-avatar')));
        await _settle(t);
        await _check(t, '$d | profile-sheet | sheet', errors);
        handle.dispose();
      });

      testWidgets('command palette', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const HomePage(), dir);
        await t.tap(find.byKey(const ValueKey('header-search')));
        await _settle(t);
        await _check(t, '$d | command-palette | sheet', errors);
        handle.dispose();
      });

      testWidgets('onboarding coach tour', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        _phone(t);
        await t.pumpWidget(fakeHost(const HomePage(), direction: dir, overrides: fakeOverrides(tourSeen: false)));
        await _settle(t);
        await _check(t, '$d | onboarding-tour | dialog', errors);
        expect(find.byType(OnboardingTour), findsOneWidget);
        handle.dispose();
      });

      testWidgets('switch workspace sheet', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const HomePage(), dir);
        await t.tap(find.byKey(const ValueKey('header-avatar')));
        await _settle(t);
        await t.tap(find.byKey(const ValueKey('profile-switch')));
        await _settle(t);
        await _check(t, '$d | workspace-sheet | sheet', errors);
        handle.dispose();
      });

      testWidgets('notifications sheet', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, Scaffold(body: Builder(builder: (c) => Center(child: TextButton(onPressed: () => showNotificationsSheet(c), child: const Text('open'))))), dir);
        await t.tap(find.text('open'));
        await _settle(t);
        await _check(t, '$d | notifications-sheet | sheet', errors);
        handle.dispose();
      });

      testWidgets('settings', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const SettingsPage(), dir);
        await _check(t, '$d | settings | page', errors);
        handle.dispose();
      });

      testWidgets('settings: sign-out dialog', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        await _open(t, const SettingsPage(), dir);
        final signOut = find.text('Sign out');
        await t.scrollUntilVisible(signOut, 200, scrollable: find.byType(Scrollable).first);
        await t.tap(signOut.last);
        await _settle(t);
        await _check(t, '$d | settings | sign-out dialog', errors);
        handle.dispose();
      });

      testWidgets('login', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        _phone(t);
        await t.pumpWidget(fakeHost(const LoginPage(), direction: dir, overrides: fakeOverrides(session: const SessionState(AuthStatus.signedOut))));
        await _settle(t);
        await _check(t, '$d | login | page', errors);
        handle.dispose();
      });

      testWidgets('workspace picker', (t) async {
        final handle = t.ensureSemantics();
        final errors = _catchErrors();
        _phone(t);
        await t.pumpWidget(fakeHost(
          const WorkspacePage(),
          direction: dir,
          overrides: fakeOverrides(session: const SessionState(AuthStatus.signedOut), pending: const PendingWorkspaceSelection('sel', fakeWorkspaces)),
        ));
        await _settle(t);
        await _check(t, '$d | workspace-picker | page', errors);
        handle.dispose();
      });
    });
  }
}
