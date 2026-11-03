import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import 'motion.dart';

class ErpSegment<T> {
  const ErpSegment({required this.value, required this.label, this.icon});

  final T value;
  final String label;
  final IconData? icon;
}

/// A segmented control whose selected "thumb" slides to the tapped segment. 48dp tall; follows text direction.
class ErpSegmented<T> extends StatelessWidget {
  const ErpSegmented({super.key, required this.segments, required this.selected, required this.onChanged});

  final List<ErpSegment<T>> segments;
  final T selected;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final n = segments.length;
    final index = segments.indexWhere((s) => s.value == selected);
    final x = n <= 1 || index < 0 ? -1.0 : -1 + 2 * index / (n - 1);
    return Container(
      height: 48,
      padding: const EdgeInsets.all(ErpSpace.s1),
      decoration: BoxDecoration(color: t.sunken, borderRadius: BorderRadius.circular(ErpRadius.card)),
      child: Stack(children: [
        if (index >= 0)
          AnimatedAlign(
            key: const ValueKey('segmented-thumb'),
            alignment: AlignmentDirectional(x, 0),
            duration: ErpMotion.of(context),
            curve: ErpMotion.curve,
            child: FractionallySizedBox(
              widthFactor: 1 / n,
              heightFactor: 1,
              child: Container(
                decoration: BoxDecoration(
                  color: dark ? t.border : t.surface,
                  borderRadius: BorderRadius.circular(ErpRadius.input),
                  boxShadow: dark ? null : ErpShadow.card,
                ),
              ),
            ),
          ),
        Row(children: [
          for (final s in segments)
            Expanded(
              child: Semantics(
                button: true,
                selected: s.value == selected,
                label: s.label,
                excludeSemantics: true,
                child: InkWell(
                  key: ValueKey('segment-${s.label.toLowerCase()}'),
                  borderRadius: BorderRadius.circular(ErpRadius.input),
                  onTap: () {
                    if (s.value == selected) return;
                    HapticFeedback.selectionClick();
                    onChanged(s.value);
                  },
                  child: Center(
                    child: AnimatedDefaultTextStyle(
                      duration: ErpMotion.of(context, ErpMotion.fast),
                      style: TextStyle(
                        fontFamily: ErpFonts.family,
                        fontSize: ErpType.bodySmall,
                        fontWeight: s.value == selected ? FontWeight.w700 : FontWeight.w500,
                        color: s.value == selected ? t.text : t.muted,
                      ),
                      child: Row(mainAxisSize: MainAxisSize.min, children: [
                        if (s.icon != null) ...[
                          Icon(s.icon, size: 18, color: s.value == selected ? t.brand : t.muted),
                          const SizedBox(width: ErpSpace.s15),
                        ],
                        Flexible(child: Text(s.label, maxLines: 1, overflow: TextOverflow.ellipsis)),
                      ]),
                    ),
                  ),
                ),
              ),
            ),
        ]),
      ]),
    );
  }
}
