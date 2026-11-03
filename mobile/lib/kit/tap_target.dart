import 'package:flutter/material.dart';

/// Touch-target sizes for the mobile app, in logical pixels (dp).
///
/// * [min] — every tappable thing: Material and Android both ask for 48x48dp.
/// * [primary] — the main action of a screen (Save, Sign in, Post, Approve): 56dp, easy to hit with a thumb.
/// * [gap] — space between two neighbouring targets, so a thumb does not hit the wrong one.
abstract final class ErpTap {
  static const double min = 48;
  static const double primary = 56;
  static const double gap = 8;

  /// Smallest size for a normal target.
  static const Size minSize = Size(min, min);

  /// Smallest size for a primary action (full width is set by the caller).
  static const Size primarySize = Size(primary, primary);

  /// For `IconButton(style: ...)` / `IconButton.styleFrom`: at least 48x48 even with a small icon, padded hit area.
  static final ButtonStyle iconButtonStyle = IconButton.styleFrom(
    minimumSize: minSize,
    tapTargetSize: MaterialTapTargetSize.padded,
  );

  /// For `TextButton`, `OutlinedButton`, `FilledButton` in rows and dialogs: 48dp tall, padded hit area.
  static const ButtonStyle buttonStyle = ButtonStyle(
    minimumSize: WidgetStatePropertyAll(Size(64, min)),
    tapTargetSize: MaterialTapTargetSize.padded,
  );

  /// For the main action of a screen: 56dp tall.
  static const ButtonStyle primaryButtonStyle = ButtonStyle(
    minimumSize: WidgetStatePropertyAll(Size(64, primary)),
    tapTargetSize: MaterialTapTargetSize.padded,
  );

  /// Constraints for `IconButton(constraints: ...)` when the theme's style is not used.
  static const BoxConstraints iconConstraints = BoxConstraints(minWidth: min, minHeight: min);
}

/// A tappable area that is never smaller than [ErpTap.min] (or [ErpTap.primary] when [primary] is true), whatever the
/// size of [child]. The visual stays as drawn and is centred; the ink and the hit area cover the whole box.
///
/// Every target needs a [label] for screen readers. Add a [tooltip] when the child is only an icon.
///
/// ```dart
/// // An 18px clear icon inside a search field still gets a 48x48 hit area.
/// ErpTapTarget(
///   label: 'Clear search',
///   tooltip: 'Clear search',
///   onTap: controller.clear,
///   child: const Icon(Icons.close, size: 18),
/// )
///
/// // A primary action in the thumb zone.
/// ErpTapTarget(primary: true, label: 'Save', onTap: save, child: const Text('Save'))
/// ```
class ErpTapTarget extends StatelessWidget {
  const ErpTapTarget({
    super.key,
    required this.label,
    required this.onTap,
    required this.child,
    this.tooltip,
    this.primary = false,
    this.onLongPress,
    this.borderRadius = const BorderRadius.all(Radius.circular(12)),
    this.selected,
    this.enabled = true,
  });

  /// What the target does, read by screen readers ("Clear search", "Next card").
  final String label;

  /// Shown on long press; recommended for icon-only targets.
  final String? tooltip;

  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final Widget child;

  /// 56dp instead of 48dp, for the main action of a screen.
  final bool primary;

  /// Shape of the ink splash.
  final BorderRadius borderRadius;

  /// For toggles and tabs: reported to screen readers.
  final bool? selected;

  final bool enabled;

  double get _side => primary ? ErpTap.primary : ErpTap.min;

  @override
  Widget build(BuildContext context) {
    final active = enabled && (onTap != null || onLongPress != null);
    Widget target = ConstrainedBox(
      constraints: BoxConstraints(minWidth: _side, minHeight: _side),
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          onTap: active ? onTap : null,
          onLongPress: active ? onLongPress : null,
          borderRadius: borderRadius,
          // The hit area is the whole ConstrainedBox, not just the visual.
          child: ConstrainedBox(
            constraints: BoxConstraints(minWidth: _side, minHeight: _side),
            child: Center(widthFactor: 1, heightFactor: 1, child: child),
          ),
        ),
      ),
    );
    if (tooltip != null) target = Tooltip(message: tooltip!, excludeFromSemantics: true, child: target);
    return Semantics(
      button: true,
      enabled: active,
      selected: selected,
      label: label,
      excludeSemantics: true,
      onTap: active ? onTap : null,
      onLongPress: active ? onLongPress : null,
      child: target,
    );
  }
}
