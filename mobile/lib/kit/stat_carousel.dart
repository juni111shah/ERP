import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import 'stat_card.dart';

/// Carousel of [ErpStatCard]s: pages of [perPage] cards laid out as an
/// [ErpStatGrid] ([columns] wide, 2x2 by default), with page snapping, swipe,
/// previous / next arrows and dot indicators. No scrollbar.
///
/// - The carousel is as tall as its tallest page, so pages never jump in height.
/// - Arrows are disabled on the first / last page; controls are hidden when
///   everything fits on one page.
/// - RTL: [PageView] and the control row follow [Directionality], so page 1
///   starts on the right and the chevrons mirror (they match text direction).
/// - Give it a new [Key] to reset it to page 1 (e.g. when a filter changes).
///
/// ```dart
/// ErpStatCarousel(key: ValueKey(category), children: cards)
/// ```
class ErpStatCarousel extends StatefulWidget {
  const ErpStatCarousel({
    super.key,
    required this.children,
    this.perPage = 4,
    this.columns = 2,
    this.spacing = 10,
    this.previousLabel = 'Previous',
    this.nextLabel = 'Next',
    this.pageLabel = _defaultPageLabel,
    this.onPageChanged,
  });

  final List<Widget> children;

  /// Cards per page (mockup: 4 as 2x2).
  final int perPage;
  final int columns;
  final double spacing;

  /// Tooltips / screen-reader labels (pass localized strings).
  final String previousLabel;
  final String nextLabel;
  final String Function(int page, int count) pageLabel;

  final ValueChanged<int>? onPageChanged;

  static String _defaultPageLabel(int page, int count) => 'Page $page of $count';

  @override
  State<ErpStatCarousel> createState() => _ErpStatCarouselState();
}

class _ErpStatCarouselState extends State<ErpStatCarousel> {
  final _controller = PageController();
  int _page = 0;

  static const _duration = Duration(milliseconds: 280);

  int get _pageCount => widget.children.isEmpty ? 0 : (widget.children.length + widget.perPage - 1) ~/ widget.perPage;

  List<Widget> _pageChildren(int page) {
    final start = page * widget.perPage;
    final end = (start + widget.perPage).clamp(0, widget.children.length);
    return widget.children.sublist(start, end);
  }

  Widget _grid(int page) => ErpStatGrid(columns: widget.columns, spacing: widget.spacing, children: _pageChildren(page));

  @override
  void didUpdateWidget(covariant ErpStatCarousel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (_page >= _pageCount && _pageCount > 0) {
      _page = _pageCount - 1;
      if (_controller.hasClients) _controller.jumpToPage(_page);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _go(int page) {
    if (page < 0 || page >= _pageCount) return;
    _controller.animateToPage(page, duration: _duration, curve: Curves.easeOutCubic);
  }

  @override
  Widget build(BuildContext context) {
    final count = _pageCount;
    if (count == 0) return const SizedBox.shrink();
    if (count == 1) return _grid(0);

    final dark = Theme.of(context).brightness == Brightness.dark;
    final brand = dark ? ErpDark.brand : ErpColors.brand;
    final idle = dark ? ErpDark.border : ErpColors.border;

    // Every page is laid out invisibly to size the carousel to the tallest one;
    // the PageView then fills that box. Only the PageView is visible / tappable.
    final sizer = ExcludeSemantics(
      child: IgnorePointer(
        child: Visibility(
          key: const ValueKey('erp-stat-carousel-sizer'),
          visible: false,
          maintainSize: true,
          maintainAnimation: true,
          maintainState: true,
          child: Stack(children: [for (var p = 0; p < count; p++) _grid(p)]),
        ),
      ),
    );

    final pages = PageView.builder(
      controller: _controller,
      itemCount: count,
      // Snapping pages, no scrollbar on any platform.
      scrollBehavior: ScrollConfiguration.of(context).copyWith(scrollbars: false, overscroll: false),
      onPageChanged: (p) {
        setState(() => _page = p);
        widget.onPageChanged?.call(p);
      },
      itemBuilder: (context, p) => Semantics(
        container: true,
        label: widget.pageLabel(p + 1, count),
        child: Align(alignment: AlignmentDirectional.topStart, child: _grid(p)),
      ),
    );

    final controls = Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        IconButton(
          key: const ValueKey('erp-stat-carousel-prev'),
          tooltip: widget.previousLabel,
          onPressed: _page > 0 ? () => _go(_page - 1) : null,
          icon: const Icon(Icons.chevron_left_rounded),
          color: brand,
          disabledColor: idle,
        ),
        Expanded(
          child: Semantics(
            liveRegion: true,
            label: widget.pageLabel(_page + 1, count),
            child: ExcludeSemantics(
              child: Wrap(
                alignment: WrapAlignment.center,
                spacing: 6,
                runSpacing: 6,
                children: [
                  for (var i = 0; i < count; i++)
                    AnimatedContainer(
                      key: ValueKey('erp-stat-carousel-dot-$i'),
                      duration: _duration,
                      curve: Curves.easeOutCubic,
                      width: i == _page ? 18 : 6,
                      height: 6,
                      decoration: BoxDecoration(
                        color: i == _page ? brand : idle,
                        borderRadius: BorderRadius.circular(ErpRadius.pill),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
        IconButton(
          key: const ValueKey('erp-stat-carousel-next'),
          tooltip: widget.nextLabel,
          onPressed: _page < count - 1 ? () => _go(_page + 1) : null,
          icon: const Icon(Icons.chevron_right_rounded),
          color: brand,
          disabledColor: idle,
        ),
      ],
    );

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Stack(children: [sizer, Positioned.fill(child: pages)]),
        const SizedBox(height: ErpSpace.s1),
        controls,
      ],
    );
  }
}
