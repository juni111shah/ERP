import 'dart:async';

import 'package:flutter/foundation.dart' show mapEquals;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/expand_search.dart';
import '../../kit/status_tag.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../kit/skeleton.dart';
import '../../kit/state_view.dart';

typedef ItemBuilder = Widget Function(BuildContext context, Map<String, dynamic> item);

/// A searchable, pull-to-refresh list that loads the next page of a list endpoint as the user scrolls.
class PagedListView extends ConsumerStatefulWidget {
  const PagedListView({super.key, required this.path, required this.itemBuilder, this.searchHint = 'Search', this.emptyText = 'Nothing here yet.', this.query = const {}, this.header, this.onClearFilter});

  final String path;
  final ItemBuilder itemBuilder;
  final String searchHint;
  final String emptyText;

  /// Extra query parameters sent with every page (for example `{'filter': 'status:eq:issued'}`); the list reloads when they change.
  final Map<String, dynamic> query;

  /// Shown at the start of the toolbar row (for example quick-filter chips); the search icon sits at its end and
  /// takes the whole row while open.
  final Widget? header;

  /// When a filter (e.g. a status chip) is applied: offered as "Clear filter" on the empty state.
  final VoidCallback? onClearFilter;

  @override
  ConsumerState<PagedListView> createState() => _PagedListViewState();
}

class _PagedListViewState extends ConsumerState<PagedListView> {
  static const _pageSize = 25;

  final _items = <Map<String, dynamic>>[];
  final _scroll = ScrollController();
  Timer? _debounce;
  String _search = '';
  bool _searchOpen = false;
  int _searchKey = 0; // bumped to rebuild the search field empty and closed
  int _page = 0;
  bool _hasNext = true;
  bool _loading = false;
  String? _error;
  int _generation = 0; // ignores answers to requests that a newer search or refresh replaced

  @override
  void initState() {
    super.initState();
    _scroll.addListener(() {
      if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 300) _loadMore();
    });
    _loadMore();
  }

  @override
  void didUpdateWidget(covariant PagedListView old) {
    super.didUpdateWidget(old);
    if (old.path != widget.path || !mapEquals(old.query, widget.query)) _reset();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _reset() async {
    _generation++;
    setState(() {
      _items.clear();
      _page = 0;
      _hasNext = true;
      _loading = false;
      _error = null;
    });
    await _loadMore();
  }

  Future<void> _loadMore() async {
    if (_loading || !_hasNext) return;
    final generation = _generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await ref.read(apiProvider).get(widget.path, query: {
        'page': _page + 1,
        'pageSize': _pageSize,
        ...widget.query,
        if (_search.isNotEmpty) 'search': _search,
      });
      if (!mounted || generation != _generation) return;
      final paged = Paged.fromJson(data as Map<String, dynamic>);
      setState(() {
        _items.addAll(paged.items);
        _page = paged.page;
        _hasNext = paged.hasNext;
      });
    } on ApiException catch (e) {
      if (!mounted || generation != _generation) return;
      setState(() => _error = e.message);
    } finally {
      if (mounted && generation == _generation) setState(() => _loading = false);
    }
  }

  void _clearSearch() {
    _debounce?.cancel();
    setState(() {
      _search = '';
      _searchOpen = false;
      _searchKey++;
    });
    _reset();
  }

  void _onSearch(String text) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      if (text.trim() == _search) return;
      _search = text.trim();
      _reset();
    });
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _reset()); // something was saved elsewhere: reload
    final search = ExpandSearch(
      key: ValueKey('paged-search-$_searchKey'),
      hintText: widget.searchHint,
      openTooltip: widget.searchHint,
      clearTooltip: 'Clear',
      initiallyExpanded: _searchOpen,
      onChanged: _onSearch,
      onOpened: () => setState(() => _searchOpen = true),
      onClosed: () => setState(() => _searchOpen = false),
    );
    return Column(
      children: [
        Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(0, ErpSpace.s1, ErpSpace.s2, ErpSpace.s1),
          child: Row(
            children: [
              if (!_searchOpen) Expanded(child: widget.header ?? const SizedBox.shrink()),
              if (_searchOpen)
                Expanded(child: Padding(padding: const EdgeInsetsDirectional.only(start: ErpSpace.s4), child: search))
              else
                SizedBox(width: 48, child: search),
            ],
          ),
        ),
        Expanded(child: _body(context)),
      ],
    );
  }

  Widget _body(BuildContext context) {
    if (_items.isEmpty && _loading) return const ErpSkeleton.list();
    if (_items.isEmpty && _error != null) return ErrorView(message: _error!, onRetry: _reset);
    if (_items.isEmpty) {
      final Widget state;
      if (_search.isNotEmpty) {
        state = ErpStateView.empty(
          key: const ValueKey('list-empty-search'),
          icon: Icons.search_off_rounded,
          title: 'No matches',
          message: 'Nothing matches "$_search".',
          actionLabel: 'Clear search',
          actionIcon: Icons.close_rounded,
          onAction: _clearSearch,
        );
      } else if (widget.onClearFilter != null) {
        state = ErpStateView.empty(
          key: const ValueKey('list-empty-filtered'),
          icon: Icons.filter_alt_off_outlined,
          title: 'Nothing in this filter',
          message: widget.emptyText,
          actionLabel: 'Clear filter',
          actionIcon: Icons.close_rounded,
          onAction: widget.onClearFilter,
        );
      } else {
        state = ErpStateView.empty(key: const ValueKey('list-empty'), title: 'Nothing here yet', message: widget.emptyText);
      }
      return RefreshIndicator(
        onRefresh: _reset,
        child: ListView(physics: const AlwaysScrollableScrollPhysics(), children: [state]),
      );
    }
    return RefreshIndicator(
      onRefresh: _reset,
      child: ListView.builder(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsetsDirectional.only(top: ErpSpace.s1, bottom: MediaQuery.paddingOf(context).bottom + 88),
        itemCount: _items.length + 1,
        itemBuilder: (context, i) {
          if (i < _items.length) return ListItemCard(child: widget.itemBuilder(context, _items[i]));
          if (_error != null) return ErrorView(message: _error!, onRetry: _loadMore, compact: true);
          return _hasNext
              ? const Padding(padding: EdgeInsets.all(ErpSpace.s4), child: Center(child: CircularProgressIndicator(strokeWidth: 2)))
              : const SizedBox(height: ErpSpace.s6);
        },
      ),
    );
  }
}

/// Wraps a list row in the card look (radius 12, hairline border, 16 side margin). Rows that already draw their own
/// card (`ErpListCard`) are passed through unchanged.
class ListItemCard extends StatelessWidget {
  const ListItemCard({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (child is ListCardRow) return Padding(padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4), child: child);
    final t = ErpTheme.of(context);
    return Padding(
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s2),
      child: Material(
        color: t.surface,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.card), side: BorderSide(color: t.borderSubtle)),
        clipBehavior: Clip.antiAlias,
        child: ListTileTheme.merge(contentPadding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s35), child: child),
      ),
    );
  }
}

/// Marker for list rows that draw their own card, so [ListItemCard] does not wrap them twice.
mixin ListCardRow on Widget {}

/// A failed load with a Retry button; a thin wrapper over [ErpStateView.error] kept for existing call sites.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry, this.compact = false});

  final String message;
  final VoidCallback onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return ErpStateView.error(key: const ValueKey('error-view'), message: message, onRetry: onRetry, compact: compact);
  }
}

/// A status pill for a document status, drawn as an Ant-style [StatusTag]. The [color] picks the tone (green = success,
/// orange = warning, red = error, blue = info, grey = neutral); other colours get a tinted pill of their own.
class StatusChip extends StatelessWidget {
  const StatusChip(this.label, {super.key, required this.color});

  final String label;
  final Color color;

  /// Colours for the statuses shared by invoices, bills and receipts.
  static Color forStatus(String status) => switch (status) {
        'paid' => ErpTone.success.fg,
        'partlyPaid' => ErpTone.warning.fg,
        'issued' => ErpTone.info.fg,
        'voided' => ErpTone.error.fg,
        'draft' => ErpTone.neutral.fg,
        _ => const Color(0xFF607D8B),
      };

  /// The tone closest to [c] by hue; null for a saturated colour with no matching tone (purple, teal...).
  static ErpTone? toneOf(Color c) {
    for (final t in ErpTone.values) {
      if (t.fg == c) return t;
    }
    final hsl = HSLColor.fromColor(c);
    if (hsl.saturation < 0.25) return ErpTone.neutral;
    final h = hsl.hue;
    if (h < 18 || h >= 335) return ErpTone.error;
    if (h < 60) return ErpTone.warning;
    if (h >= 75 && h < 165) return ErpTone.success;
    if (h >= 195 && h < 250) return ErpTone.info;
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final tone = toneOf(color);
    if (tone != null) return StatusTag(label: label, tone: tone);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final fg = dark ? Color.lerp(color, Colors.white, 0.45)! : color;
    return Container(
      height: 22,
      padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s2),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.10), borderRadius: BorderRadius.circular(ErpRadius.pill), border: Border.all(color: color.withValues(alpha: 0.35))),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Container(width: 6, height: 6, decoration: BoxDecoration(color: fg, shape: BoxShape.circle)),
        const SizedBox(width: 5),
        Flexible(child: Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: ErpType.caption, height: 1.2, fontWeight: FontWeight.w600, color: fg))),
      ]),
    );
  }
}
