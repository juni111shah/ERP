import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// One tile in a [ModuleGrid] group.
@immutable
class ModuleTile {
  const ModuleTile({required this.label, required this.icon, this.badgeCount, this.onTap, this.note});

  final String label;
  final IconData icon;

  /// Red count badge on the icon; hidden when null or <= 0. Shows "99+" above 99.
  final int? badgeCount;
  final VoidCallback? onTap;

  /// Small muted line under the label (for example "web" for areas only the web portal has). A tile without [onTap]
  /// is drawn dimmed.
  final String? note;
}

/// A titled section of tiles. [accent] tints the icon square (defaults to brand green).
@immutable
class ModuleGroup {
  const ModuleGroup({required this.title, required this.tiles, this.accent});

  final String title;
  final List<ModuleTile> tiles;
  final Color? accent;
}

/// Server-/data-driven "More" menu: each group is a section header followed by a 3-column grid of tiles.
///
/// Non-scrolling (shrink-wrapped) so it can sit inside a page's ListView / CustomScrollView.
class ModuleGrid extends StatelessWidget {
  const ModuleGrid({
    super.key,
    required this.groups,
    this.columns = 3,
    this.padding = const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4),
  });

  final List<ModuleGroup> groups;
  final int columns;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final visible = groups.where((g) => g.tiles.isNotEmpty).toList();
    return Padding(
      padding: padding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final g in visible) ...[
            ModuleSectionHeader(g.title),
            _TileRows(group: g, columns: columns),
          ],
        ],
      ),
    );
  }
}

/// Small uppercase muted section label ("FINANCE", "OPERATIONS"...).
class ModuleSectionHeader extends StatelessWidget {
  const ModuleSectionHeader(this.title, {super.key});

  final String title;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsetsDirectional.only(top: ErpSpace.s4, bottom: ErpSpace.s2),
        child: Text(
          title.toUpperCase(),
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: ErpColors.muted,
                letterSpacing: 0.6,
                fontWeight: FontWeight.w600,
              ),
        ),
      );
}

class _TileRows extends StatelessWidget {
  const _TileRows({required this.group, required this.columns});

  final ModuleGroup group;
  final int columns;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    final tiles = group.tiles;
    for (var i = 0; i < tiles.length; i += columns) {
      final cells = <Widget>[];
      for (var c = 0; c < columns; c++) {
        if (c > 0) cells.add(const SizedBox(width: ErpSpace.s2));
        final idx = i + c;
        cells.add(Expanded(
          child: idx < tiles.length
              ? ModuleTileCard(tile: tiles[idx], accent: group.accent ?? ErpColors.brand)
              : const SizedBox.shrink(),
        ));
      }
      rows.add(Padding(
        padding: EdgeInsetsDirectional.only(top: i == 0 ? 0 : ErpSpace.s2),
        child: IntrinsicHeight(child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: cells)),
      ));
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: rows);
  }
}

/// A single tile: icon in a tinted rounded square (with optional red badge at its top-end corner) over a label.
class ModuleTileCard extends StatelessWidget {
  const ModuleTileCard({super.key, required this.tile, this.accent = ErpColors.brand});

  final ModuleTile tile;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final count = tile.badgeCount ?? 0;
    final radius = BorderRadius.circular(ErpRadius.card);
    final tileWidget = Material(
      color: ErpColors.surface,
      shape: RoundedRectangleBorder(borderRadius: radius, side: const BorderSide(color: ErpColors.borderSubtle)),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: tile.onTap,
        child: Padding(
          padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s2, vertical: ErpSpace.s3),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    alignment: AlignmentDirectional.center,
                    decoration: BoxDecoration(
                      color: accent.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(ErpRadius.input),
                    ),
                    child: Icon(tile.icon, size: 20, color: accent),
                  ),
                  if (count > 0)
                    PositionedDirectional(
                      top: -6,
                      end: -8,
                      child: _CountBadge(count: count),
                    ),
                ],
              ),
              const SizedBox(height: ErpSpace.s2),
              Text(
                tile.label,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpColors.text, fontWeight: FontWeight.w500),
              ),
              if (tile.note != null)
                Text(tile.note!, style: const TextStyle(fontSize: ErpType.micro, color: ErpColors.muted, fontWeight: FontWeight.w600, letterSpacing: 0.3)),
            ],
          ),
        ),
      ),
    );
    return tile.onTap == null ? Opacity(opacity: 0.55, child: tileWidget) : tileWidget;
  }
}

class _CountBadge extends StatelessWidget {
  const _CountBadge({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) => Container(
        constraints: const BoxConstraints(minWidth: 18, minHeight: 18),
        padding: const EdgeInsetsDirectional.symmetric(horizontal: 5),
        alignment: AlignmentDirectional.center,
        decoration: BoxDecoration(
          color: ErpColors.danger,
          borderRadius: BorderRadius.circular(ErpRadius.pill),
          border: Border.all(color: ErpColors.surface, width: 1.5),
        ),
        child: Text(
          count > 99 ? '99+' : '$count',
          textDirection: TextDirection.ltr,
          style: const TextStyle(color: ErpColors.surface, fontSize: ErpType.micro, fontWeight: FontWeight.w700, height: 1.2),
        ),
      );
}
