import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

/// Opens the sheet holding the actions that do not fit in a detail screen's bar, when it has any.
/// The bar stays in the tree, so every action of the screen can then be found at once.
Future<void> openMoreActions(WidgetTester tester) async {
  final more = find.byKey(const ValueKey('detail-more'));
  if (more.evaluate().isEmpty) return;
  await tester.tap(more);
  await tester.pumpAndSettle();
}
