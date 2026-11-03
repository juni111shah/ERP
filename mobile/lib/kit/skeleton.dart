import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import 'motion.dart';

/// Sweeps a soft highlight across its child's opaque pixels (the [ErpBone]s inside). No packages: one
/// [AnimationController] and a [ShaderMask]. Still (no sweep) under reduced motion.
class ErpShimmer extends StatefulWidget {
  const ErpShimmer({super.key, required this.child});

  final Widget child;

  @override
  State<ErpShimmer> createState() => _ErpShimmerState();
}

class _ErpShimmerState extends State<ErpShimmer> with SingleTickerProviderStateMixin {
  late final _controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 1300));

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (ErpMotion.reduced(context)) {
      _controller.stop();
    } else if (!_controller.isAnimating) {
      _controller.repeat();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (ErpMotion.reduced(context)) return widget.child;
    final t = ErpTheme.of(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final highlight = dark ? Color.lerp(t.sunken, Colors.white, 0.08)! : Color.lerp(t.sunken, Colors.white, 0.7)!;
    return AnimatedBuilder(
      animation: _controller,
      child: widget.child,
      builder: (context, child) => ShaderMask(
        blendMode: BlendMode.srcATop,
        shaderCallback: (bounds) => LinearGradient(
          colors: [t.sunken, highlight, t.sunken],
          stops: const [0.35, 0.5, 0.65],
          begin: Alignment.centerLeft,
          end: Alignment.centerRight,
          transform: _Slide(_controller.value),
        ).createShader(bounds),
        child: child,
      ),
    );
  }
}

class _Slide extends GradientTransform {
  const _Slide(this.t);

  final double t;

  @override
  Matrix4 transform(Rect bounds, {TextDirection? textDirection}) => Matrix4.translationValues(bounds.width * (t * 2 - 1), 0, 0);
}

/// One placeholder shape.
class ErpBone extends StatelessWidget {
  const ErpBone({super.key, this.width, required this.height, this.radius = 6, this.circle = false});

  final double? width;
  final double height;
  final double radius;
  final bool circle;

  @override
  Widget build(BuildContext context) => Container(
        width: circle ? height : width,
        height: height,
        decoration: BoxDecoration(
          color: ErpTheme.of(context).sunken,
          shape: circle ? BoxShape.circle : BoxShape.rectangle,
          borderRadius: circle ? null : BorderRadius.circular(radius),
        ),
      );
}

enum _Kind { overview, list, detail, notifications }

/// Page loaders shaped like the content they stand in for, with a shimmer. Replaces full-page spinners.
///
/// ```dart
/// loading: () => const ErpSkeleton.list()
/// ```
class ErpSkeleton extends StatelessWidget {
  const ErpSkeleton.overview({super.key}) : _kind = _Kind.overview, count = 0;
  const ErpSkeleton.list({super.key, this.count = 6}) : _kind = _Kind.list;
  const ErpSkeleton.detail({super.key}) : _kind = _Kind.detail, count = 0;
  const ErpSkeleton.notifications({super.key, this.count = 4}) : _kind = _Kind.notifications;

  final _Kind _kind;
  final int count;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final body = switch (_kind) {
      _Kind.overview => _overview(),
      _Kind.list => _list(t),
      _Kind.detail => _detail(t),
      _Kind.notifications => _notifications(),
    };
    return Semantics(
      label: 'Loading',
      container: true,
      child: ExcludeSemantics(child: ErpShimmer(child: body)),
    );
  }

  static Widget _card(ErpTheme t, Widget child, {EdgeInsetsGeometry padding = const EdgeInsets.all(ErpSpace.s35)}) => Container(
        padding: padding,
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: BorderRadius.circular(ErpRadius.card),
          border: Border.all(color: t.borderSubtle),
        ),
        child: child,
      );

  Widget _overview() {
    Widget card() => const Expanded(child: ErpBone(height: 100, radius: ErpRadius.card));
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.all(ErpSpace.s4),
      children: [
        const Row(children: [
          ErpBone(width: 64, height: 32, radius: ErpRadius.pill),
          SizedBox(width: ErpSpace.s2),
          ErpBone(width: 84, height: 32, radius: ErpRadius.pill),
          SizedBox(width: ErpSpace.s2),
          ErpBone(width: 72, height: 32, radius: ErpRadius.pill),
        ]),
        const SizedBox(height: ErpSpace.s4),
        const Align(alignment: AlignmentDirectional.centerStart, child: ErpBone(width: 120, height: 10, radius: 4)),
        const SizedBox(height: ErpSpace.s3),
        Row(children: [card(), const SizedBox(width: ErpSpace.s25), card()]),
        const SizedBox(height: ErpSpace.s25),
        Row(children: [card(), const SizedBox(width: ErpSpace.s25), card()]),
        const SizedBox(height: ErpSpace.s4),
        const ErpBone(height: 260, radius: ErpRadius.panel),
      ],
    );
  }

  Widget _list(ErpTheme t) => ListView.separated(
        key: const ValueKey('list-skeleton'),
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s1, ErpSpace.s4, ErpSpace.s4),
        itemCount: count,
        separatorBuilder: (_, _) => const SizedBox(height: ErpSpace.s2),
        itemBuilder: (_, i) => _card(
          t,
          Row(children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                ErpBone(width: i.isEven ? 150 : 120, height: 14),
                const SizedBox(height: ErpSpace.s2),
                ErpBone(width: i.isEven ? 96 : 130, height: 10),
              ]),
            ),
            const Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
              ErpBone(width: 72, height: 14),
              SizedBox(height: ErpSpace.s2),
              ErpBone(width: 52, height: 18, radius: ErpRadius.pill),
            ]),
          ]),
        ),
      );

  Widget _detail(ErpTheme t) => ListView(
        key: const ValueKey('detail-skeleton'),
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.all(ErpSpace.s4),
        children: [
          _card(
            t,
            padding: const EdgeInsets.all(ErpSpace.s4),
            const Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [ErpBone(width: 90, height: 12), Spacer(), ErpBone(width: 60, height: 20, radius: ErpRadius.pill)]),
              SizedBox(height: ErpSpace.s3),
              ErpBone(width: 180, height: 28),
              SizedBox(height: ErpSpace.s2),
              ErpBone(width: 140, height: 12),
              SizedBox(height: ErpSpace.s4),
              ErpBone(height: 6, radius: ErpRadius.pill),
            ]),
          ),
          const SizedBox(height: ErpSpace.s3),
          for (var i = 0; i < 2; i++) ...[
            _card(
              t,
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const ErpBone(width: 80, height: 10),
                for (var r = 0; r < 4; r++) ...[
                  const SizedBox(height: ErpSpace.s3),
                  Row(children: [ErpBone(width: 70.0 + r * 8, height: 12), const Spacer(), ErpBone(width: 100.0 - r * 10, height: 12)]),
                ],
              ]),
            ),
            const SizedBox(height: ErpSpace.s3),
          ],
        ],
      );

  Widget _notifications() => Column(
        key: const ValueKey('notifications-skeleton'),
        children: [
          for (var i = 0; i < count; i++)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4, vertical: ErpSpace.s25),
              child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const ErpBone(height: 36, circle: true),
                const SizedBox(width: ErpSpace.s3),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    ErpBone(width: i.isEven ? 180 : 140, height: 13),
                    const SizedBox(height: ErpSpace.s2),
                    const ErpBone(height: 10),
                    const SizedBox(height: ErpSpace.s15),
                    const ErpBone(width: 70, height: 9),
                  ]),
                ),
              ]),
            ),
        ],
      );
}
