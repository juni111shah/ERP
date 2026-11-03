import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/app_theme.dart';
import 'package:erp_mobile/theme/tokens.dart';

/// One quick-filter chip. [label] is already localized; [count] is optional.
@immutable
class FilterChipItem<T> {
  const FilterChipItem({
    required this.value,
    required this.label,
    this.count,
    this.semanticsLabel,
  });

  final T value;
  final String label;
  final int? count;

  /// Optional screen-reader text, e.g. "Overdue, 7 invoices".
  final String? semanticsLabel;
}

/// Horizontally scrolling quick-filter row (mockup `.chips`): an optional
/// leading filter-icon chip (opens the full filter sheet) followed by
/// pill chips with counts. Selected chip: brandSubtle background, brand text,
/// brand border at 35% opacity, white count badge.
///
/// RTL: the row starts at the start edge and scrolls the mirrored way;
/// counts are forced LTR so digits never reorder.
class FilterChipsRow<T> extends StatelessWidget {
  const FilterChipsRow({
    super.key,
    required this.items,
    required this.selected,
    required this.onSelected,
    this.onFilterTap,
    this.filterTooltip,
    this.filterActive = false,
    this.formatCount,
    this.padding = const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4),
  });

  final List<FilterChipItem<T>> items;

  /// Currently selected value (null = none).
  final T? selected;

  final ValueChanged<T> onSelected;

  /// When set, a leading filter-icon chip is shown and calls this.
  final VoidCallback? onFilterTap;

  /// Tooltip / semantics for the filter icon (localized). Required for a11y
  /// when [onFilterTap] is set.
  final String? filterTooltip;

  /// Highlight the filter icon (e.g. advanced filters applied).
  final bool filterActive;

  /// Optional number formatter for counts (e.g. NumberFormat.decimalPattern).
  /// Defaults to `toString()` (Latin digits).
  final String Function(int count)? formatCount;

  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: padding,
        children: [
          if (onFilterTap != null) ...[
            _FilterIconChip(
              onTap: onFilterTap!,
              tooltip: filterTooltip,
              active: filterActive,
            ),
            const SizedBox(width: ErpSpace.s2),
          ],
          for (var i = 0; i < items.length; i++) ...[
            if (i > 0) const SizedBox(width: ErpSpace.s2),
            _Chip<T>(
              item: items[i],
              selected: items[i].value == selected,
              onTap: () => onSelected(items[i].value),
              formatCount: formatCount,
            ),
          ],
        ],
      ),
    );
  }
}

BoxDecoration _chipDecoration(BuildContext context, bool on) {
  final t = ErpTheme.of(context);
  return BoxDecoration(
    color: on ? t.brandSubtle : t.surface,
    borderRadius: BorderRadius.circular(ErpRadius.pill),
    border: Border.all(color: on ? t.brand.withValues(alpha: 0.35) : t.border),
  );
}

class _Chip<T> extends StatelessWidget {
  const _Chip({
    required this.item,
    required this.selected,
    required this.onTap,
    this.formatCount,
  });

  final FilterChipItem<T> item;
  final bool selected;
  final VoidCallback onTap;
  final String Function(int count)? formatCount;

  @override
  Widget build(BuildContext context) {
    final count = item.count;
    final t = ErpTheme.of(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    return Semantics(
      button: true,
      selected: selected,
      label: item.semanticsLabel ??
          (count == null ? item.label : '${item.label} ${formatCount?.call(count) ?? count}'),
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        customBorder: const StadiumBorder(),
        // 48dp tap target around a 32dp visual chip.
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48, minWidth: 48),
          child: Center(
            child: Container(
              height: 32,
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s3),
              decoration: _chipDecoration(context, selected),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    item.label,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                      color: selected ? t.brand : t.text2,
                    ),
                  ),
                  if (count != null) ...[
                    const SizedBox(width: 6),
                    Container(
                      padding: const EdgeInsetsDirectional.symmetric(horizontal: 6),
                      decoration: BoxDecoration(
                        color: selected ? t.surface : (dark ? const Color(0x1FFFFFFF) : const Color(0x0F000000)),
                        borderRadius: BorderRadius.circular(ErpRadius.pill),
                      ),
                      child: Text(
                        formatCount?.call(count) ?? '$count',
                        textDirection: TextDirection.ltr,
                        style: TextStyle(
                          fontSize: 11,
                          height: 16 / 11,
                          color: selected ? t.brand : t.text2,
                          fontFeatures: ErpFonts.tabular,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _FilterIconChip extends StatelessWidget {
  const _FilterIconChip({required this.onTap, this.tooltip, this.active = false});

  final VoidCallback onTap;
  final String? tooltip;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final chip = Semantics(
      button: true,
      label: tooltip,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        customBorder: const StadiumBorder(),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48, minWidth: 48),
          child: Center(
            child: Container(
              height: 32,
              padding: const EdgeInsetsDirectional.symmetric(horizontal: 10),
              decoration: _chipDecoration(context, active),
              child: Icon(
                Icons.filter_alt_outlined,
                size: 16,
                color: active ? t.brand : t.text2,
              ),
            ),
          ),
        ),
      ),
    );
    return tooltip == null ? chip : Tooltip(message: tooltip!, child: chip);
  }
}
