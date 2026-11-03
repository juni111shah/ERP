import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/me.dart';
import '../features/common/paged_list_view.dart';
import 'field.dart';
import 'form_page.dart';

/// A list endpoint as a screen: search, paging, optional status chips, and a "+" button that opens a create form when the user may create.
class ListScreen extends ConsumerStatefulWidget {
  const ListScreen({
    super.key,
    required this.title,
    required this.path,
    required this.itemBuilder,
    this.create,
    this.createPermission,
    this.searchHint = 'Search',
    this.emptyText = 'Nothing here yet.',
    this.filters = const [],
    this.filterKey = 'status',
    this.query = const {},
    this.showAppBar = true,
  });

  final String title;
  final String path;
  final ItemBuilder itemBuilder;

  /// Builds the create form; the "+" button is shown when [createPermission] (if any) is held.
  final FormSpec Function(WidgetRef ref)? create;
  final String? createPermission;
  final String searchHint;
  final String emptyText;

  /// Status chips; the chosen one is sent as `filter=<filterKey>:eq:<value>`.
  final List<Option> filters;
  final String filterKey;
  final Map<String, dynamic> query;
  final bool showAppBar;

  @override
  ConsumerState<ListScreen> createState() => _ListScreenState();
}

class _ListScreenState extends ConsumerState<ListScreen> {
  String? _filter;

  @override
  Widget build(BuildContext context) {
    final me = ref.watch(meOrEmptyProvider);
    final canCreate = widget.create != null && me.can(widget.createPermission);
    final query = {...widget.query, if (_filter != null) 'filter': '${widget.filterKey}:eq:$_filter'};
    return Scaffold(
      appBar: widget.showAppBar ? AppBar(title: Text(widget.title)) : null,
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () => openForm<Object?>(context, widget.create!(ref)),
              icon: const Icon(Icons.add),
              label: const Text('New'),
            )
          : null,
      body: Column(
        children: [
          if (widget.filters.isNotEmpty)
            SizedBox(
              height: 48,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                children: [
                  Padding(padding: const EdgeInsets.only(right: 8), child: ChoiceChip(label: const Text('All'), selected: _filter == null, onSelected: (_) => setState(() => _filter = null))),
                  for (final o in widget.filters)
                    Padding(padding: const EdgeInsets.only(right: 8), child: ChoiceChip(label: Text(o.label), selected: _filter == o.value, onSelected: (_) => setState(() => _filter = o.value))),
                ],
              ),
            ),
          Expanded(child: PagedListView(path: widget.path, itemBuilder: widget.itemBuilder, searchHint: widget.searchHint, emptyText: widget.emptyText, query: query)),
        ],
      ),
    );
  }
}
