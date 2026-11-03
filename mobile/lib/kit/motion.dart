import 'dart:math' as math;

import 'package:flutter/material.dart';

/// Motion tokens. Every transition stays within 150-300ms and collapses to nothing when the platform asks for reduced
/// motion (`MediaQuery.disableAnimations`, e.g. Android "Remove animations").
class ErpMotion {
  ErpMotion._();

  static const fast = Duration(milliseconds: 150);
  static const medium = Duration(milliseconds: 220);
  static const slow = Duration(milliseconds: 300);

  static const curve = Curves.easeOutCubic;

  /// Spring-like: a fast start that settles softly, without overshooting (sheets must not lift off their edge).
  static const spring = Cubic(0.16, 1, 0.3, 1);

  static bool reduced(BuildContext context) => MediaQuery.maybeDisableAnimationsOf(context) ?? false;

  /// [d], or zero when reduced motion is on.
  static Duration of(BuildContext context, [Duration d = medium]) => reduced(context) ? Duration.zero : d;
}

/// Scales its child down slightly while a pointer is pressed on it. It does not handle the tap itself, so the child's
/// own [InkWell] keeps its ripple and semantics.
class ErpPressable extends StatefulWidget {
  const ErpPressable({super.key, required this.child, this.enabled = true, this.scale = 0.97});

  final Widget child;
  final bool enabled;
  final double scale;

  @override
  State<ErpPressable> createState() => _ErpPressableState();
}

class _ErpPressableState extends State<ErpPressable> {
  bool _down = false;

  void _set(bool v) {
    if (_down != v && mounted) setState(() => _down = v);
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.enabled || ErpMotion.reduced(context)) return widget.child;
    return Listener(
      onPointerDown: (_) => _set(true),
      onPointerUp: (_) => _set(false),
      onPointerCancel: (_) => _set(false),
      child: AnimatedScale(
        scale: _down ? widget.scale : 1,
        duration: ErpMotion.fast,
        curve: ErpMotion.curve,
        child: widget.child,
      ),
    );
  }
}

/// Fades and slides its child in once, when it is first built. Give it a new key to replay. [index] staggers a list of
/// them (15ms apart, capped so the last one still finishes within 300ms).
class ErpFadeSlideIn extends StatelessWidget {
  const ErpFadeSlideIn({super.key, required this.child, this.index = 0, this.offset = 12, this.duration = ErpMotion.medium});

  final Widget child;
  final int index;

  /// Start offset in logical pixels, downwards.
  final double offset;
  final Duration duration;

  @override
  Widget build(BuildContext context) {
    if (ErpMotion.reduced(context)) return child;
    final delay = math.min(index, 5) * 15;
    final total = math.min(duration.inMilliseconds + delay, ErpMotion.slow.inMilliseconds);
    final start = delay / total;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: Duration(milliseconds: total),
      curve: Interval(start, 1, curve: ErpMotion.curve),
      builder: (context, v, child) => Opacity(
        opacity: v,
        child: Transform.translate(offset: Offset(0, (1 - v) * offset), child: child),
      ),
      child: child,
    );
  }
}

/// Briefly scales its child up and back whenever [trigger] changes (e.g. a badge count).
class ErpPulse extends StatefulWidget {
  const ErpPulse({super.key, required this.trigger, required this.child, this.amount = 0.25});

  final Object? trigger;
  final Widget child;
  final double amount;

  @override
  State<ErpPulse> createState() => _ErpPulseState();
}

class _ErpPulseState extends State<ErpPulse> with SingleTickerProviderStateMixin {
  late final _controller = AnimationController(vsync: this, duration: ErpMotion.slow);

  @override
  void didUpdateWidget(covariant ErpPulse old) {
    super.didUpdateWidget(old);
    if (old.trigger != widget.trigger && !ErpMotion.reduced(context)) _controller.forward(from: 0);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
        animation: _controller,
        builder: (context, child) => Transform.scale(scale: 1 + widget.amount * math.sin(math.pi * _controller.value), child: child),
        child: widget.child,
      );
}
