import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import 'intro_flag.dart';
import 'intro_illustrations.dart';
import 'intro_motion.dart';
import 'intro_scene.dart';
import 'intro_strings.dart';

/// First-open intro tour (5 swipeable screens, light theme only). Skip, Get started and system back all mark it seen;
/// the router then moves on to sign-in (or home).
class IntroPage extends ConsumerStatefulWidget {
  const IntroPage({super.key});

  @override
  ConsumerState<IntroPage> createState() => _IntroPageState();
}

class _IntroPageState extends ConsumerState<IntroPage> with SingleTickerProviderStateMixin {
  static const _count = 5;

  final _pages = PageController();
  final _scenes = introScenes();

  /// Seconds since the active page became active; drives entrance + drift. One ticker for the whole tour.
  final _seconds = ValueNotifier<double>(0);
  late final Ticker _ticker;
  final _visited = <int>{0};
  int _active = 0;
  bool _done = false;
  bool _reduce = false;

  static final double _stopAt =
      [IntroMotion.totalMs / 1000, IntroMotion.driftWindowSeconds].reduce((a, b) => a > b ? a : b);

  @override
  void initState() {
    super.initState();
    _ticker = createTicker(_onTick);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final reduce = IntroMotion.reduce(context);
    if (reduce != _reduce || (!reduce && !_ticker.isActive && _seconds.value == 0)) {
      _reduce = reduce;
      _restart();
    }
  }

  void _onTick(Duration elapsed) {
    final s = elapsed.inMicroseconds / 1e6;
    if (s >= _stopAt) {
      _seconds.value = _stopAt;
      _ticker.stop();
    } else {
      _seconds.value = s;
    }
  }

  void _restart() {
    if (_ticker.isActive) _ticker.stop();
    if (_reduce) {
      _seconds.value = _stopAt;
    } else {
      _seconds.value = 0;
      _ticker.start();
    }
  }

  @override
  void dispose() {
    _ticker.dispose();
    _seconds.dispose();
    _pages.dispose();
    super.dispose();
  }

  void _onPageChanged(int i) {
    setState(() {
      _active = i;
      _visited.add(i);
    });
    _restart();
  }

  void _next() {
    if (_active >= _count - 1) {
      _finish();
      return;
    }
    if (_reduce) {
      _pages.jumpToPage(_active + 1);
    } else {
      _pages.animateToPage(_active + 1, duration: IntroMotion.pageTurn, curve: IntroMotion.pageCurve);
    }
  }

  Future<void> _finish() async {
    if (_done) return;
    _done = true;
    await ref.read(introProvider.notifier).markSeen();
  }

  @override
  Widget build(BuildContext context) {
    final s = IntroStrings.of(context);
    final last = _active == _count - 1;
    return Theme(
      data: AppTheme.light(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: PopScope(
          canPop: false,
          onPopInvokedWithResult: (didPop, _) {
            if (!didPop) _finish();
          },
          child: Scaffold(
            backgroundColor: ErpColors.canvas,
            body: SafeArea(
              child: Column(children: [
                Padding(
                  padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s5, ErpSpace.s1, ErpSpace.s2, 0),
                  child: Row(children: [
                    Expanded(
                      child: Text(
                        s.step(_active + 1, _count),
                        style: const TextStyle(fontSize: ErpType.caption, fontWeight: FontWeight.w600, color: ErpColors.muted),
                      ),
                    ),
                    TextButton(
                      onPressed: _finish,
                      style: TextButton.styleFrom(minimumSize: const Size(64, 48), foregroundColor: ErpColors.text2),
                      child: Text(s.skip, style: const TextStyle(fontSize: ErpType.body, fontWeight: FontWeight.w600)),
                    ),
                  ]),
                ),
                Expanded(
                  child: PageView.builder(
                    controller: _pages,
                    itemCount: _count,
                    onPageChanged: _onPageChanged,
                    itemBuilder: (context, i) => _IntroScreen(
                      index: i,
                      scene: _scenes[i],
                      title: s.titles[i],
                      body: s.bodies[i],
                      label: s.illustrationLabel,
                      pages: _pages,
                      seconds: _seconds,
                      active: i == _active,
                      visited: _visited.contains(i),
                      reduce: _reduce,
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s5, ErpSpace.s2, ErpSpace.s5, ErpSpace.s4),
                  child: last
                      ? SizedBox(
                          width: double.infinity,
                          height: 52,
                          child: FilledButton(
                            onPressed: _finish,
                            style: FilledButton.styleFrom(
                              backgroundColor: ErpColors.brand,
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.panel)),
                            ),
                            child: Text(s.getStarted, style: const TextStyle(fontSize: ErpType.bodyLarge, fontWeight: FontWeight.w600)),
                          ),
                        )
                      : Row(children: [
                          _PageDots(count: _count, active: _active, reduce: _reduce),
                          const Spacer(),
                          SizedBox(
                            height: 52,
                            child: FilledButton(
                              onPressed: _next,
                              style: FilledButton.styleFrom(
                                backgroundColor: ErpColors.brand,
                                minimumSize: const Size(120, 52),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.panel)),
                              ),
                              child: Row(mainAxisSize: MainAxisSize.min, children: [
                                Text(s.next, style: const TextStyle(fontSize: ErpType.bodyLarge, fontWeight: FontWeight.w600)),
                                const SizedBox(width: ErpSpace.s15),
                                const Icon(Icons.arrow_forward_rounded, size: 18),
                              ]),
                            ),
                          ),
                        ]),
                ),
              ]),
            ),
          ),
        ),
      ),
    );
  }
}

class _IntroScreen extends StatelessWidget {
  const _IntroScreen({
    required this.index,
    required this.scene,
    required this.title,
    required this.body,
    required this.label,
    required this.pages,
    required this.seconds,
    required this.active,
    required this.visited,
    required this.reduce,
  });

  final int index;
  final IntroSceneSpec scene;
  final String title;
  final String body;
  final String label;
  final PageController pages;
  final ValueNotifier<double> seconds;
  final bool active;
  final bool visited;
  final bool reduce;

  double _offset() {
    if (!pages.hasClients || !pages.position.haveDimensions) return 0;
    return ((pages.page ?? index.toDouble()) - index).clamp(-1.0, 1.0);
  }

  @override
  Widget build(BuildContext context) {
    final rtl = Directionality.of(context) == TextDirection.rtl;
    final listen = Listenable.merge([pages, if (active) seconds]);
    Widget content = Column(children: [
      Expanded(
        child: LayoutBuilder(builder: (context, box) {
          final scale = [box.maxWidth / introStageSize.width, box.maxHeight / introStageSize.height].reduce((a, b) => a < b ? a : b);
          final pageWidth = scale > 0 ? box.maxWidth / scale : introStageSize.width;
          return Semantics(
            label: label,
            image: true,
            child: ExcludeSemantics(
              child: RepaintBoundary(
                child: Center(
                  child: FittedBox(
                    child: Directionality(
                      // Sample UI; its pieces are laid out in mockup (LTR) coordinates, the rig still mirrors for RTL.
                      textDirection: TextDirection.ltr,
                      child: AnimatedBuilder(
                        animation: listen,
                        builder: (context, _) {
                          final secs = active ? seconds.value : (visited ? IntroMotion.driftWindowSeconds : 0.0);
                          return IntroScene(
                              spec: scene,
                              timeline: active || visited ? (secs * 1000 / IntroMotion.totalMs).clamp(0.0, 1.0) : 0,
                              seconds: active ? secs : IntroMotion.driftWindowSeconds,
                              pageOffset: _offset(),
                              pageWidth: pageWidth,
                              reduceMotion: reduce,
                              rtl: rtl,
                          );
                        },
                      ),
                    ),
                  ),
                ),
              ),
            ),
          );
        }),
      ),
      Padding(
        padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s6, ErpSpace.s3, ErpSpace.s6, 0),
        child: AnimatedBuilder(
          animation: listen,
          builder: (context, _) {
            final timeline = reduce || visited && !active ? 1.0 : (active ? (seconds.value * 1000 / IntroMotion.totalMs).clamp(0.0, 1.0) : 0.0);
            Widget line(int i, Widget child) {
              if (reduce) return child;
              final t = IntroMotion.interval(IntroLayer.caption, index: i).transform(timeline);
              return Opacity(
                opacity: IntroMotion.opacityFor(t),
                child: Transform(
                  transform: IntroMotion.layerTransform(IntroLayer.caption, t, 0, 0, rtl: rtl),
                  child: child,
                ),
              );
            }

            return SizedBox(
              width: double.infinity,
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                line(
                  0,
                  Text(
                    title,
                    style: const TextStyle(fontSize: ErpType.headline, fontWeight: FontWeight.w700, letterSpacing: -0.3, height: 1.25, color: ErpColors.text),
                  ),
                ),
                const SizedBox(height: ErpSpace.s2),
                line(1, Text(body, style: const TextStyle(fontSize: ErpType.bodyLarge, height: 1.45, color: ErpColors.muted))),
              ]),
            );
          },
        ),
      ),
    ]);
    if (reduce && active) {
      // Reduced motion: no 3D entrance, a short crossfade instead.
      content = TweenAnimationBuilder<double>(
        key: ValueKey('fade$index'),
        tween: Tween(begin: 0, end: 1),
        duration: IntroMotion.reducedFade,
        builder: (context, v, child) => Opacity(opacity: v, child: child),
        child: content,
      );
    }
    return content;
  }
}

class _PageDots extends StatelessWidget {
  const _PageDots({required this.count, required this.active, required this.reduce});

  final int count;
  final int active;
  final bool reduce;

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        for (var i = 0; i < count; i++)
          AnimatedContainer(
            duration: reduce ? Duration.zero : const Duration(milliseconds: 260),
            curve: IntroMotion.settle,
            width: i == active ? 22 : 7,
            height: 7,
            margin: const EdgeInsetsDirectional.only(end: 6),
            decoration: BoxDecoration(
              color: i == active ? ErpColors.brand : ErpColors.border,
              borderRadius: BorderRadius.circular(4),
            ),
          ),
      ]);
}
