import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/me.dart';
import '../features/common/paged_list_view.dart';
import 'field.dart';
import 'filter_chips.dart';
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
          ? Padding(
              // Inside the home shell the floating bottom bar overlaps the body (extendBody), which raises the bottom
              // padding above the system inset; lift the button by that difference so it clears the bar.
              padding: EdgeInsets.only(bottom: (MediaQuery.paddingOf(context).bottom - MediaQuery.viewPaddingOf(context).bottom).clamp(0.0, double.infinity)),
              child: FloatingActionButton.extended(
                onPressed: () => openForm<Object?>(context, widget.create!(ref)),
                icon: const Icon(Icons.add),
                label: const Text('New'),
              ),
            )
          : null,
      body: PagedListView(
        path: widget.path,
        itemBuilder: widget.itemBuilder,
        searchHint: widget.searchHint,
        emptyText: widget.emptyText,
        query: query,
        header: widget.filters.isEmpty
            ? null
            : FilterChipsRow<String>(
                items: [const FilterChipItem(value: '', label: 'All'), for (final o in widget.filters) FilterChipItem(value: o.value, label: o.label)],
                selected: _filter ?? '',
                onSelected: (v) => setState(() => _filter = v.isEmpty ? null : v),
              ),
      ),
    );
  }
}
