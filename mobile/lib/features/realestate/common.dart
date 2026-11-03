import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/api_client.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart' show Json;
import '../../kit/field.dart';
import '../common/paged_list_view.dart';

const rePath = '/api/v1/m/realestate';

const reUnitStatuses = [Option('vacant', 'Vacant'), Option('reserved', 'Reserved'), Option('occupied', 'Occupied'), Option('unavailable', 'Unavailable')];
const rePriorities = [Option('low', 'Low'), Option('normal', 'Normal'), Option('high', 'High'), Option('critical', 'Critical')];
const reUsages = [Option('residential', 'Residential'), Option('commercial', 'Commercial')];

/// Status colours for the real estate statuses (the kit only knows the invoice ones).
Color reStatusColor(String status) => switch (status) {
      'active' || 'occupied' || 'completed' || 'published' || 'won' || 'collected' || 'agentPaid' || 'paid' => Colors.green.shade700,
      'vacant' || 'new' || 'assigned' || 'agreed' || 'invoiced' => Colors.blue.shade700,
      'reserved' || 'inProgress' || 'onHold' || 'holdingOver' || 'scheduled' || 'negotiating' || 'negotiation' || 'viewing' || 'contacted' => Colors.orange.shade800,
      'terminated' || 'cancelled' || 'lost' || 'unavailable' || 'damaged' || 'poor' => Colors.red.shade700,
      'draft' || 'ended' || 'renewed' || 'closed' => Colors.grey.shade700,
      _ => Colors.blueGrey,
    };

Widget reChip(Object? status) {
  final s = (status ?? '').toString();
  return StatusChip(humanize(s), color: reStatusColor(s));
}

void reGo(BuildContext context, Widget screen) => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => screen));

Field propertyLookup({bool required = true, String key = 'propertyId'}) =>
    Field.lookup(key, 'Property', path: '$rePath/properties', subtitleKey: 'code', required: required);

Field unitLookup({bool required = false, String key = 'unitId', String label = 'Unit'}) =>
    Field.lookup(key, label, path: '$rePath/units', labelKey: 'unitNumber', subtitleKey: 'propertyName', required: required);

Field tenantLookup({String key = 'lesseePartyId', String label = 'Tenant', bool required = true}) =>
    Field.lookup(key, label, path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: required);

Field bankLookup({String key = 'bankAccountId', String label = 'Bank account'}) =>
    Field.lookup(key, label, path: '/api/v1/finance/accounts', subtitleKey: 'code', required: true);

List<Option> entityOptions(Me me) => [for (final e in me.legalEntities) Option(e.id, e.name)];

bool reCan(WidgetRef ref, String perm) => ref.read(meOrEmptyProvider).can(perm);

/// A two line list tile with a status chip at the end.
Widget reTile(BuildContext context, {required String title, String? subtitle, String? trailing, Object? status, required VoidCallback onTap}) => ListTile(
      onTap: onTap,
      title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: subtitle == null ? null : Text(subtitle, maxLines: 2, overflow: TextOverflow.ellipsis),
      isThreeLine: subtitle != null && subtitle.contains('\n'),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (trailing != null) Text(trailing, style: const TextStyle(fontWeight: FontWeight.w600)),
          if (status != null) ...[const SizedBox(height: 4), reChip(status)],
        ],
      ),
    );

/// A short, non-paged list of related records (a property's units, a lease's work orders...) loaded from a list or array endpoint.
class RelatedList extends ConsumerStatefulWidget {
  const RelatedList({super.key, required this.path, required this.tile, this.query, this.empty = 'Nothing here.', this.pick});

  final String path;
  final Map<String, dynamic>? query;
  final Widget Function(BuildContext, Json) tile;
  final String empty;

  /// Picks the list out of an answer that is an object (for example `rows`).
  final String? pick;

  @override
  ConsumerState<RelatedList> createState() => _RelatedListState();
}

class _RelatedListState extends ConsumerState<RelatedList> {
  List<Json>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      var data = await ref.read(apiProvider).get(widget.path, query: widget.query);
      if (data is Map && widget.pick != null) data = data[widget.pick];
      if (data is Map && data['items'] is List) data = data['items'];
      if (mounted) setState(() => _items = [for (final e in (data is List ? data : const [])) (e as Map).cast<String, dynamic>()]);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    if (_error != null) return Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Text(_error!));
    final items = _items;
    if (items == null) return const Padding(padding: EdgeInsets.all(12), child: Center(child: CircularProgressIndicator(strokeWidth: 2)));
    if (items.isEmpty) return Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Text(widget.empty));
    return Column(children: [for (final i in items) widget.tile(context, i)]);
  }
}
