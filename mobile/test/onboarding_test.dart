import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/plain_labels.dart';
import 'package:erp_mobile/features/onboarding/onboarding_tour.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Host app (like the real app: no extra locales; RTL picks the Arabic strings) with a button that calls [onPressed] with a context under the Navigator.
Widget host(
  void Function(BuildContext context) onPressed, {
  TextDirection direction = TextDirection.ltr,
  ThemeData? theme,
}) {
  return MaterialApp(
    theme: theme ?? AppTheme.light(),
    builder: (context, child) => Directionality(textDirection: direction, child: child!),
    home: Builder(
      builder: (context) => Scaffold(
        body: Center(child: TextButton(onPressed: () => onPressed(context), child: const Text('open'))),
      ),
    ),
  );
}

const _seen = OnboardingTourStore.seenKey;
final _next = find.byKey(const ValueKey('onboarding-next'));
final _skip = find.byKey(const ValueKey('onboarding-skip'));
final _tour = find.byKey(const ValueKey('onboarding-tour'));

void main() {
  testWidgets('steps through all four steps, Back works, Done closes and saves the flag', (tester) async {
    final store = MemoryKeyValueStore();
    OnboardingOutcome? outcome;
    await tester.pumpWidget(host((c) async => outcome = await OnboardingTour.show(c, store)));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(_tour, findsOneWidget);
    expect(find.text('Step 1 of 4'), findsOneWidget);
    expect(find.text('Your key numbers'), findsOneWidget);
    expect(find.byKey(const ValueKey('onboarding-back')), findsNothing);

    await tester.tap(_next);
    await tester.pumpAndSettle();
    expect(find.text('Step 2 of 4'), findsOneWidget);
    expect(find.text('Swipe for more'), findsOneWidget);

    await tester.tap(find.byKey(const ValueKey('onboarding-back')));
    await tester.pumpAndSettle();
    expect(find.text('Step 1 of 4'), findsOneWidget);

    // Swiping the pages also advances.
    await tester.drag(find.byKey(const ValueKey('onboarding-pages')), const Offset(-400, 0));
    await tester.pumpAndSettle();
    expect(find.text('Step 2 of 4'), findsOneWidget);

    await tester.tap(_next);
    await tester.pumpAndSettle();
    expect(find.text('Your profile menu'), findsOneWidget);
    await tester.tap(_next);
    await tester.pumpAndSettle();
    expect(find.text('Jump anywhere'), findsOneWidget);
    expect(find.text('Done'), findsOneWidget);

    await tester.tap(_next);
    await tester.pumpAndSettle();
    expect(_tour, findsNothing);
    expect(outcome, OnboardingOutcome.completed);
    expect(store.values[_seen], '1');
  });

  testWidgets('Skip closes from the first step and saves the flag (default on)', (tester) async {
    final store = MemoryKeyValueStore();
    OnboardingOutcome? outcome;
    await tester.pumpWidget(host((c) async => outcome = await OnboardingTour.show(c, store)));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    await tester.tap(_skip);
    await tester.pumpAndSettle();
    expect(_tour, findsNothing);
    expect(outcome, OnboardingOutcome.skipped);
    expect(await OnboardingTourStore(store).isSeen(), isTrue);
  });

  testWidgets("unticking Don't show again leaves the flag unset", (tester) async {
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(host((c) => OnboardingTour.show(c, store)));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    final checkbox = find.byType(Checkbox);
    expect(tester.widget<Checkbox>(checkbox).value, isTrue);
    await tester.tap(find.byKey(const ValueKey('onboarding-dont-show')));
    await tester.pump();
    expect(tester.widget<Checkbox>(checkbox).value, isFalse);

    await tester.tap(_skip);
    await tester.pumpAndSettle();
    expect(store.values.containsKey(_seen), isFalse);
  });

  testWidgets('maybeShowAfterSignIn shows once, then not again', (tester) async {
    final store = MemoryKeyValueStore();
    final shown = <bool>[];
    await tester.pumpWidget(host((c) async => shown.add(await OnboardingTour.maybeShowAfterSignIn(c, store))));

    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    expect(_tour, findsOneWidget);
    await tester.tap(_skip);
    await tester.pumpAndSettle();
    expect(shown, [true]);

    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    expect(_tour, findsNothing);
    expect(shown, [true, false]);
  });

  testWidgets('scope keeps a separate flag per user', (tester) async {
    final store = MemoryKeyValueStore()..values['${_seen}_u1'] = '1';
    expect(await OnboardingTourStore(store, scope: 'u1').isSeen(), isTrue);
    expect(await OnboardingTourStore(store, scope: 'u2').isSeen(), isFalse);
    expect(await OnboardingTourStore(store).isSeen(), isFalse);
  });

  testWidgets('reset clears the flag; ShowTourAgainTile reopens the tour', (tester) async {
    final store = MemoryKeyValueStore()..values[_seen] = '1';
    await OnboardingTour.reset(store);
    expect(store.values.containsKey(_seen), isFalse);
    store.values[_seen] = '1';

    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.light(),
      home: Builder(
        builder: (hostContext) => Scaffold(
          body: Center(
            child: TextButton(
              onPressed: () => showModalBottomSheet<void>(
                context: hostContext,
                builder: (_) => ShowTourAgainTile(store: store, hostContext: hostContext),
              ),
              child: const Text('profile'),
            ),
          ),
        ),
      ),
    ));
    await tester.tap(find.text('profile'));
    await tester.pumpAndSettle();
    final tile = find.byKey(const ValueKey('profile-tour'));
    expect(find.text('Show the tour again'), findsOneWidget);
    expect(tester.getSize(tile).height, greaterThanOrEqualTo(48));

    await tester.tap(tile);
    await tester.pumpAndSettle();
    expect(find.byType(ShowTourAgainTile), findsNothing); // sheet closed
    expect(_tour, findsOneWidget);
    expect(find.text('Step 1 of 4'), findsOneWidget);

    await tester.tap(_skip);
    await tester.pumpAndSettle();
    expect(store.values[_seen], '1');
  });

  testWidgets('RTL + Arabic: Arabic text, Skip at the start-end edge, buttons ≥48dp, swipe follows RTL', (tester) async {
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(host((c) => OnboardingTour.show(c, store), direction: TextDirection.rtl));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.text('الخطوة 1 من 4'), findsOneWidget);
    expect(find.text(OnboardingStrings.ar.steps.first.title), findsOneWidget);
    expect(find.text('تخطٍّ'), findsOneWidget);

    // Skip sits at the end edge: left of the progress text in RTL.
    final skipX = tester.getCenter(_skip).dx;
    final progressX = tester.getCenter(find.byKey(const ValueKey('onboarding-progress'))).dx;
    expect(skipX, lessThan(progressX));

    expect(tester.getSize(_next).height, greaterThanOrEqualTo(48));
    expect(tester.getSize(_skip).height, greaterThanOrEqualTo(48));

    // In RTL the next page comes from the left: drag right to advance.
    await tester.drag(find.byKey(const ValueKey('onboarding-pages')), const Offset(400, 0));
    await tester.pumpAndSettle();
    expect(find.text('الخطوة 2 من 4'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.tap(_skip);
    await tester.pumpAndSettle();
    expect(store.values[_seen], '1');
  });

  testWidgets('renders in dark mode without errors', (tester) async {
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(host((c) => OnboardingTour.show(c, store), theme: AppTheme.dark()));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    expect(_tour, findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.tap(_skip);
    await tester.pumpAndSettle();
  });

  test('PlainLabels: lookup, Arabic, case-insensitive, unknown passthrough', () {
    expect(PlainLabels.lookup('Receivables'), 'Money owed to you');
    expect(PlainLabels.lookup('AR ageing'), 'Overdue by age');
    expect(PlainLabels.lookup('kpi'), 'Key numbers');
    expect(PlainLabels.lookup('  Journals '), 'Manual entries');
    expect(PlainLabels.lookup('Receivables', languageCode: 'ar'), 'أموال مستحقة لك');
    expect(PlainLabels.lookup('Invoice'), isNull);
    expect(PlainLabels.plain('Invoice'), 'Invoice');
    expect(PlainLabels.entry('Vendor')?.ar, 'مورّد');
    for (final e in PlainLabels.terms.entries) {
      expect(e.value.en, isNotEmpty, reason: e.key);
      expect(e.value.ar, isNotEmpty, reason: e.key);
    }
  });
}
