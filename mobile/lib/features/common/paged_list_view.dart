import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../../core/session.dart';

typedef ItemBuilder = Widget Function(BuildContext context, Map<String, dynamic> item);

/// A searchable, pull-to-refresh list that loads the next page of a list endpoint as the user scrolls.
class PagedListView extends ConsumerStatefulWidget {
  const PagedListView({super.key, required this.path, required this.itemBuilder, this.searchHint = 'Search', this.emptyText = 'Nothing here yet.'});

  final String path;
  final ItemBuilder itemBuilder;
  final String searchHint;
  final String emptyText;

  @override
  ConsumerState<PagedListView> createState() => _PagedListViewState();
}

class _PagedListViewState extends ConsumerState<PagedListView> {
  static const _pageSize = 25;

  final _items = <Map<String, dynamic>>[];
  final _scroll = ScrollController();
  Timer? _debounce;
  String _search = '';
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
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
          child: SearchBar(
            hintText: widget.searchHint,
            leading: const Icon(Icons.search),
            elevation: const WidgetStatePropertyAll(0),
            onChanged: _onSearch,
          ),
        ),
        Expanded(child: _body(context)),
      ],
    );
  }

  Widget _body(BuildContext context) {
    if (_items.isEmpty && _loading) return const Center(child: CircularProgressIndicator());
    if (_items.isEmpty && _error != null) return ErrorView(message: _error!, onRetry: _reset);
    if (_items.isEmpty) {
      return RefreshIndicator(
        onRefresh: _reset,
        child: ListView(children: [Padding(padding: const EdgeInsets.all(48), child: Center(child: Text(widget.emptyText)))]),
      );
    }
    return RefreshIndicator(
      onRefresh: _reset,
      child: ListView.separated(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: _items.length + 1,
        separatorBuilder: (_, _) => const Divider(height: 1),
        itemBuilder: (context, i) {
          if (i < _items.length) return widget.itemBuilder(context, _items[i]);
          if (_error != null) return ErrorView(message: _error!, onRetry: _loadMore, compact: true);
          return _hasNext
              ? const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator(strokeWidth: 2)))
              : const SizedBox(height: 24);
        },
      ),
    );
  }
}

class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry, this.compact = false});

  final String message;
  final VoidCallback onRetry;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final content = Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.cloud_off_outlined, size: compact ? 24 : 48, color: Theme.of(context).colorScheme.error),
        const SizedBox(height: 8),
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 8),
        OutlinedButton(onPressed: onRetry, child: const Text('Try again')),
      ],
    );
    return Padding(padding: const EdgeInsets.all(24), child: compact ? content : Center(child: content));
  }
}

/// A small coloured pill for a document status.
class StatusChip extends StatelessWidget {
  const StatusChip(this.label, {super.key, required this.color});

  final String label;
  final Color color;

  /// Colours for the statuses shared by invoices, bills and receipts.
  static Color forStatus(String status) => switch (status) {
        'paid' => Colors.green.shade700,
        'partlyPaid' => Colors.orange.shade800,
        'issued' => Colors.blue.shade700,
        'voided' => Colors.red.shade700,
        'draft' => Colors.grey.shade700,
        _ => Colors.blueGrey,
      };

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(10)),
        child: Text(label, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
      );
}
