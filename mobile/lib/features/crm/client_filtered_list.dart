import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/me.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../common/paged_list_view.dart';

/// Like the kit's ListScreen, but the status chips filter on the phone.
///
/// The CRM and Services list endpoints answer 400 "'status' cannot be filtered" (their entities have no filterable columns),
/// so a server-side `filter=` is not possible. Pages of 200 are loaded and rows that do not match the chip are hidden.
class ClientFilteredListScreen extends ConsumerStatefulWidget {
  const ClientFilteredListScreen({
    super.key,
    required this.title,
    required this.path,
    required this.itemBuilder,
    required this.filters,
    required this.filterKey,
    this.create,
    this.createPermission,
    this.searchHint = 'Search',
    this.emptyText = 'Nothing here yet.',
  });

  final String title;
  final String path;
  final ItemBuilder itemBuilder;
  final List<Option> filters;
  final String filterKey;
  final FormSpec Function(WidgetRef ref)? create;
  final String? createPermission;
  final String searchHint;
  final String emptyText;

  @override
  ConsumerState<ClientFilteredListScreen> createState() => _State();
}

class _State extends ConsumerState<ClientFilteredListScreen> {
  String? _filter;

  @override
  Widget build(BuildContext context) {
    final me = ref.watch(meOrEmptyProvider);
    final canCreate = widget.create != null && me.can(widget.createPermission);
    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, widget.create!(ref)), icon: const Icon(Icons.add), label: const Text('New'))
          : null,
      body: Column(children: [
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
        Expanded(
          child: PagedListView(
            path: widget.path,
            query: const {'pageSize': 200},
            searchHint: widget.searchHint,
            emptyText: widget.emptyText,
            itemBuilder: (context, item) => _filter == null || '${item[widget.filterKey]}' == _filter ? widget.itemBuilder(context, item) : const SizedBox.shrink(),
          ),
        ),
      ]),
    );
  }
}
