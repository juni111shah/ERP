import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/app_theme.dart';
import 'package:erp_mobile/theme/tokens.dart';

/// One destination of [ErpBottomNav].
@immutable
class ErpNavItem {
  const ErpNavItem({required this.icon, required this.activeIcon, required this.label, this.badgeCount});

  /// Outline icon shown when the tab is not selected.
  final IconData icon;

  /// Filled icon shown inside the lifted circle when selected.
  final IconData activeIcon;
  final String label;

  /// Optional red count badge on the unselected icon; hidden when null or <= 0.
  final int? badgeCount;
}

/// Floating ERP bottom navigation: a white rounded bar with a soft shadow; the selected tab lifts into a
/// brandSubtle circle that sits in a smooth concave notch cut into the bar's top edge. On change the notch and
/// circle slide to the new tab (300ms, easeOutCubic) and the icon cross-fades.
///
/// RTL: tab order and the notch position mirror (index 0 is at the right in RTL). Respects the bottom safe area.
///
/// Example (as `Scaffold.bottomNavigationBar`, with `extendBody: true` so content shows behind the margins):
/// ```dart
/// ErpBottomNav(
///   currentIndex: index,
///   onTap: (i) => setState(() => index = i),
///   items: const [
///     ErpNavItem(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Overview'),
///     ErpNavItem(icon: Icons.description_outlined, activeIcon: Icons.description, label: 'Invoices'),
///     ErpNavItem(icon: Icons.account_balance_wallet_outlined, activeIcon: Icons.account_balance_wallet, label: 'Receipts'),
///     ErpNavItem(icon: Icons.people_outline, activeIcon: Icons.people, label: 'Customers'),
///     ErpNavItem(icon: Icons.grid_view_outlined, activeIcon: Icons.grid_view_rounded, label: 'More'),
///   ],
/// )
/// ```
class ErpBottomNav extends StatelessWidget {
  const ErpBottomNav({
    super.key,
    required this.items,
    required this.currentIndex,
    required this.onTap,
    this.onLongPress,
    this.duration = const Duration(milliseconds: 300),
    this.curve = Curves.easeOutCubic,
  }) : assert(items.length >= 2, 'ErpBottomNav needs at least two items');

  final List<ErpNavItem> items;
  final int currentIndex;
  final ValueChanged<int> onTap;

  /// Optional long-press per tab (e.g. long-press Overview opens the command palette).
  final ValueChanged<int>? onLongPress;
  final Duration duration;
  final Curve curve;

  /// Diameter of the lifted circle.
  static const double circleSize = 56;

  /// Height of the white bar itself.
  static const double barHeight = 68;

  /// How far the circle rises above the bar's top edge.
  static const double lift = 20;

  /// Gap between the circle and the notch edge.
  static const double notchGap = 4;

  static const double _radius = 20;

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.paddingOf(context).bottom;
    final rtl = Directionality.of(context) == TextDirection.rtl;
    final selected = currentIndex.clamp(0, items.length - 1);

    return Padding(
      padding: EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, math.max(bottomInset, ErpSpace.s3)),
      child: SizedBox(
        height: barHeight + lift,
        child: LayoutBuilder(
          builder: (context, constraints) {
            final width = constraints.maxWidth;
            final slot = width / items.length;
            return TweenAnimationBuilder<double>(
              tween: Tween<double>(end: selected.toDouble()),
              duration: duration,
              curve: curve,
              builder: (context, pos, _) {
                // Physical x of the notch centre: mirrored in RTL so index 0 sits at the right.
                final ltrX = (pos + 0.5) * slot;
                final cx = rtl ? width - ltrX : ltrX;
                return Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Positioned.fill(
                      child: CustomPaint(
                        painter: _NotchedBarPainter(
                          notchX: cx,
                          top: lift,
                          circleCenterY: circleSize / 2,
                          notchRadius: circleSize / 2 + notchGap,
                          radius: _radius,
                          color: ErpTheme.of(context).surface,
                          shadow: Theme.of(context).brightness == Brightness.dark ? const Color(0x66000000) : const Color(0x1A101828),
                        ),
                      ),
                    ),
                    // Tabs: a Row mirrors itself in RTL, matching the notch maths above.
                    Positioned.fill(
                      top: lift,
                      child: Row(
                        children: [
                          for (var i = 0; i < items.length; i++)
                            Expanded(
                              child: _Tab(
                                item: items[i],
                                selected: i == selected,
                                onTap: () => onTap(i),
                                onLongPress: onLongPress == null ? null : () => onLongPress!(i),
                              ),
                            ),
                        ],
                      ),
                    ),
                    // The lifted circle. Positioned (physical) on purpose: cx is already direction-resolved.
                    Positioned(
                      left: cx - circleSize / 2,
                      top: 0,
                      width: circleSize,
                      height: circleSize,
                      child: IgnorePointer(
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: ErpTheme.of(context).brandSubtle,
                            shape: BoxShape.circle,
                            border: Border.all(color: ErpTheme.of(context).surface, width: 3),
                            boxShadow: const [
                              BoxShadow(color: Color(0x1F10955D), blurRadius: 10, offset: Offset(0, 3)),
                            ],
                          ),
                          child: Center(
                            child: AnimatedSwitcher(
                              duration: duration,
                              switchInCurve: curve,
                              switchOutCurve: Curves.easeIn,
                              transitionBuilder: (child, anim) => FadeTransition(
                                opacity: anim,
                                child: ScaleTransition(scale: Tween<double>(begin: 0.8, end: 1).animate(anim), child: child),
                              ),
                              child: Icon(
                                items[selected].activeIcon,
                                key: ValueKey<int>(selected),
                                size: 26,
                                color: ErpTheme.of(context).brand,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _Tab extends StatelessWidget {
  const _Tab({required this.item, required this.selected, required this.onTap, this.onLongPress});

  final ErpNavItem item;
  final bool selected;
  final VoidCallback onTap;
  final VoidCallback? onLongPress;

  @override
  Widget build(BuildContext context) {
    final base = Theme.of(context).textTheme.labelSmall ?? const TextStyle(fontSize: ErpType.label);
    final count = item.badgeCount ?? 0;
    final t = ErpTheme.of(context);
    Widget icon = Icon(item.icon, size: 22, color: t.muted);
    if (count > 0) {
      icon = Badge(
        label: Text(count > 99 ? '99+' : '$count', textDirection: TextDirection.ltr),
        backgroundColor: ErpColors.danger,
        textColor: Colors.white,
        child: icon,
      );
    }
    return Semantics(
      button: true,
      selected: selected,
      label: item.label,
      excludeSemantics: true,
      onLongPress: onLongPress,
      child: InkResponse(
        onTap: onTap,
        onLongPress: onLongPress,
        radius: 32,
        highlightShape: BoxShape.circle,
        child: Padding(
          padding: const EdgeInsetsDirectional.only(bottom: ErpSpace.s3),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              // Selected: the icon lives in the lifted circle, so keep the slot empty under the notch.
              AnimatedOpacity(
                opacity: selected ? 0 : 1,
                duration: const Duration(milliseconds: 200),
                child: icon,
              ),
              const SizedBox(height: ErpSpace.s1),
              AnimatedDefaultTextStyle(
                duration: const Duration(milliseconds: 200),
                style: selected
                    ? base.copyWith(color: t.text, fontWeight: FontWeight.w700, fontSize: ErpType.caption)
                    : base.copyWith(color: t.muted, fontWeight: FontWeight.w500, fontSize: ErpType.label),
                child: Text(item.label, maxLines: 1, overflow: TextOverflow.ellipsis, textAlign: TextAlign.center),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Paints the white rounded bar with a concave, smooth-shouldered notch centred on [notchX].
class _NotchedBarPainter extends CustomPainter {
  _NotchedBarPainter({
    required this.notchX,
    required this.top,
    required this.circleCenterY,
    required this.notchRadius,
    required this.radius,
    this.color = ErpColors.surface,
    this.shadow = const Color(0x1A101828),
  });

  final Color color;
  final Color shadow;
  final double notchX;
  final double top;
  final double circleCenterY;
  final double notchRadius;
  final double radius;

  static const double _shoulder = 14;

  @override
  void paint(Canvas canvas, Size size) {
    final bar = Path()
      ..addRRect(RRect.fromRectAndRadius(Rect.fromLTRB(0, top, size.width, size.height), Radius.circular(radius)));

    final r = notchRadius;
    final cx = notchX;
    // Where the notch circle meets a line just below the bar's top edge.
    const edgeDrop = 4.0;
    final dy = top + edgeDrop - circleCenterY;
    final dx = math.sqrt(math.max(0, r * r - dy * dy));
    final notch = Path()
      ..moveTo(cx - r - _shoulder, top - 10)
      ..lineTo(cx - r - _shoulder, top)
      ..quadraticBezierTo(cx - r, top, cx - dx, top + edgeDrop)
      ..arcToPoint(Offset(cx + dx, top + edgeDrop), radius: Radius.circular(r), largeArc: true, clockwise: false)
      ..quadraticBezierTo(cx + r, top, cx + r + _shoulder, top)
      ..lineTo(cx + r + _shoulder, top - 10)
      ..close();

    final shape = Path.combine(PathOperation.difference, bar, notch);

    canvas.drawPath(
      shape.shift(const Offset(0, 4)),
      Paint()
        ..color = shadow
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 12),
    );
    canvas.drawPath(shape, Paint()..color = color);
  }

  @override
  bool shouldRepaint(_NotchedBarPainter old) =>
      old.notchX != notchX ||
      old.top != top ||
      old.circleCenterY != circleCenterY ||
      old.notchRadius != notchRadius ||
      old.radius != radius ||
      old.color != color ||
      old.shadow != shadow;
}
