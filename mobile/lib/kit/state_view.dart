import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import 'motion.dart';

/// The one empty / error / nothing-yet state: an icon in a tinted circle, a title, a short message and an optional
/// action (Retry, Clear filter...). Used for network errors, empty lists, empty filters, no notifications and charts
/// with no data, so no screen ever looks blank or broken.
///
/// ```dart
/// ErpStateView.error(message: e.message, onRetry: reload)
/// ErpStateView.empty(title: 'No invoices', message: 'Nothing matches this filter.', actionLabel: 'Clear filter', onAction: clear)
/// ```
class ErpStateView extends StatelessWidget {
  const ErpStateView({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.actionLabel,
    this.actionIcon,
    this.onAction,
    this.tone,
    this.compact = false,
  });

  /// A load failed; [onRetry] runs the load again.
  const ErpStateView.error({super.key, this.message, required VoidCallback onRetry, this.compact = false, this.title = "Couldn't load this"})
      : icon = Icons.cloud_off_outlined,
        actionLabel = 'Retry',
        actionIcon = Icons.refresh_rounded,
        onAction = onRetry,
        tone = ErpTone.error;

  /// Nothing to show; pass an action such as "Clear filter" when the user can do something about it.
  const ErpStateView.empty({
    super.key,
    this.icon = Icons.inbox_outlined,
    required this.title,
    this.message,
    this.actionLabel,
    this.actionIcon,
    this.onAction,
    this.compact = false,
  }) : tone = null;

  final IconData icon;
  final String title;
  final String? message;
  final String? actionLabel;
  final IconData? actionIcon;
  final VoidCallback? onAction;

  /// Tints the icon circle; null uses the brand tint.
  final ErpTone? tone;

  /// Smaller, for inline use (end of a list, inside a card).
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final b = Theme.of(context).brightness;
    final size = compact ? 40.0 : 56.0;
    final hasAction = actionLabel != null && onAction != null;
    final content = ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 360),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: size,
            height: size,
            decoration: BoxDecoration(color: tone?.bgFor(b) ?? t.brandSubtle, shape: BoxShape.circle),
            child: Icon(icon, size: compact ? 20 : 28, color: tone == null ? t.brand : (tone == ErpTone.error ? Theme.of(context).colorScheme.error : tone!.fgFor(b))),
          ),
          SizedBox(height: compact ? ErpSpace.s2 : ErpSpace.s4),
          Text(
            title,
            key: const ValueKey('state-title'),
            textAlign: TextAlign.center,
            style: ErpType.titleStyle.copyWith(fontSize: compact ? ErpType.body : ErpType.title, color: t.text),
          ),
          if (message != null && message!.isNotEmpty) ...[
            const SizedBox(height: ErpSpace.s1),
            Text(message!, textAlign: TextAlign.center, style: TextStyle(fontSize: ErpType.bodySmall, height: 1.4, color: t.muted)),
          ],
          if (hasAction) ...[
            SizedBox(height: compact ? ErpSpace.s2 : ErpSpace.s4),
            OutlinedButton.icon(
              key: const ValueKey('state-action'),
              onPressed: onAction,
              icon: Icon(actionIcon ?? Icons.arrow_forward_rounded, size: 18),
              label: Text(actionLabel!),
            ),
          ],
        ],
      ),
    );
    return ErpFadeSlideIn(
      offset: 8,
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: ErpSpace.s6, vertical: compact ? ErpSpace.s4 : ErpSpace.s10),
        child: Center(child: content),
      ),
    );
  }
}
