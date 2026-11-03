import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';

const invPath = '/api/v1/m/inventory';

num _n(Object? v) => toNum(v) ?? 0;
String _q(Object? v) => formatKpi(_n(v).toDouble(), 'number');
String _m(Object? v) => formatMoney(_n(v));

const movementKinds = [Option('receipt', 'Receipt'), Option('issue', 'Issue'), Option('adjustment', 'Adjustment'), Option('transferIn', 'Transfer in'), Option('transferOut', 'Transfer out')];
const countStatuses = [Option('draft', 'Draft'), Option('counting', 'Counting'), Option('posted', 'Recorded'), Option('cancelled', 'Cancelled')];

Field _warehouse(String key, String label) => Field.lookup(key, label, path: '$invPath/warehouses', subtitleKey: 'code', required: true);
Field _product(String key, String label, {Map<String, String> copy = const {}}) =>
    Field.lookup(key, label, path: '/api/v1/items', query: const {'filter': 'type:eq:product'}, subtitleKey: 'sku', required: true, copy: copy);

// ---------------------------------------------------------------- stock forms

FormSpec receiveForm() => FormSpec(
      title: 'Receive stock',
      fields: [
        _warehouse('warehouseId', 'Warehouse'),
        _product('itemId', 'Item'),
        Field.decimal('quantity', 'Quantity', required: true, min: 0.0001),
        Field.money('unitCost', 'Unit cost', required: true),
        Field.date('date', 'Date', required: true, initial: 'today'),
        Field.lookup('offsetAccountId', 'Offset account (e.g. inventory clearing)', path: '/api/v1/finance/accounts', subtitleKey: 'code', required: true),
        Field.text('reference', 'Reference', maxLength: 100),
        Field.text('memo', 'Memo', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$invPath/stock/receive', body: body),
    );

FormSpec issueForm() => FormSpec(
      title: 'Issue stock',
      fields: [
        _warehouse('warehouseId', 'Warehouse'),
        _product('itemId', 'Item'),
        Field.decimal('quantity', 'Quantity', required: true, min: 0.0001),
        Field.date('date', 'Date', required: true, initial: 'today'),
        Field.text('reference', 'Reference', maxLength: 100),
        Field.text('memo', 'Memo', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$invPath/stock/issue', body: body),
    );

FormSpec adjustForm() => FormSpec(
      title: 'Adjust stock',
      intro: 'Enter a positive quantity to add stock or a negative one to remove it. A unit cost is used when adding.',
      fields: [
        _warehouse('warehouseId', 'Warehouse'),
        _product('itemId', 'Item'),
        Field.decimal('quantityDelta', 'Quantity change (+/-)', required: true, validator: (v, _) => _n(v) == 0 ? 'Enter a change other than zero' : null),
        Field.money('unitCost', 'Unit cost (when adding)'),
        Field.date('date', 'Date', required: true, initial: 'today'),
        Field.lookup('offsetAccountId', 'Offset account (optional)', path: '/api/v1/finance/accounts', subtitleKey: 'code'),
        Field.text('reference', 'Reference', maxLength: 100),
        Field.text('memo', 'Memo', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$invPath/stock/adjust', body: body),
    );

FormSpec transferForm() => FormSpec(
      title: 'Transfer stock',
      fields: [
        _warehouse('fromWarehouseId', 'From warehouse'),
        _warehouse('toWarehouseId', 'To warehouse'),
        _product('itemId', 'Item'),
        Field.decimal('quantity', 'Quantity', required: true, min: 0.0001),
        Field.date('date', 'Date', required: true, initial: 'today'),
        Field.text('reference', 'Reference', maxLength: 100),
        Field.text('memo', 'Memo', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$invPath/stock/transfer', body: body),
    );

FormSpec reorderPolicyForm() => FormSpec(
      title: 'Reorder level',
      fields: [
        _product('itemId', 'Item'),
        Field.decimal('reorderLevel', 'Reorder when stock falls to', required: true, min: 0),
        Field.decimal('reorderQuantity', 'Suggested order quantity', required: true, min: 0),
      ],
      submit: (ref, body, raw) async {
        await ref.read(apiProvider).put('$invPath/stock/policy', body: body);
        return {'saved': true};
      },
    );

// ---------------------------------------------------------------- stock on hand

class StockScreen extends ConsumerStatefulWidget {
  const StockScreen({super.key});

  @override
  ConsumerState<StockScreen> createState() => _StockScreenState();
}

class _StockScreenState extends ConsumerState<StockScreen> {
  List<Json> _rows = [];
  List<Json> _reorder = [];
  num _total = 0;
  String _search = '';
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final api = ref.read(apiProvider);
      final stock = await api.get('$invPath/stock') as Json;
      final reorder = await api.get('$invPath/reports/reorder') as Json;
      if (!mounted) return;
      setState(() {
        _rows = (stock['rows'] as List<dynamic>).cast<Json>();
        _total = _n(stock['totalValue']);
        _reorder = (reorder['rows'] as List<dynamic>? ?? const []).cast<Json>();
        _error = null;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(FormSpec spec) async {
    await openForm<Object?>(context, spec);
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final me = ref.watch(meOrEmptyProvider);
    final canWrite = me.can('inventory.stock.write');
    final s = _search.toLowerCase();
    final rows = _rows.where((r) => s.isEmpty || '${r['sku']} ${r['item']} ${r['warehouse']}'.toLowerCase().contains(s)).toList();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Stock on hand'),
        actions: [
          if (canWrite)
            PopupMenuButton<FormSpec Function()>(
              icon: const Icon(Icons.add),
              tooltip: 'Stock actions',
              onSelected: (b) => _open(b()),
              itemBuilder: (_) => [
                PopupMenuItem(value: receiveForm, child: const Text('Receive stock')),
                PopupMenuItem(value: issueForm, child: const Text('Issue stock')),
                PopupMenuItem(value: adjustForm, child: const Text('Adjust stock')),
                PopupMenuItem(value: transferForm, child: const Text('Transfer between warehouses')),
                PopupMenuItem(value: reorderPolicyForm, child: const Text('Set reorder level')),
              ],
            ),
        ],
      ),
      body: _loading
          ? const ErpSkeleton.detail()
          : _error != null
              ? ErrorView(message: _error!, onRetry: _load)
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
                    InfoCard(rows: [InfoRow('Total stock value', formatMoney(_total), bold: true), InfoRow('Stock lines', '${_rows.length}')]),
                    if (_reorder.isNotEmpty) ...[
                      Text('Reorder alerts', style: Theme.of(context).textTheme.titleMedium),
                      for (final r in _reorder)
                        ListTile(
                          dense: true,
                          leading: Icon(Icons.warning_amber, color: Colors.orange.shade800),
                          title: Text('${r['item']}'),
                          subtitle: Text('${r['sku']} · on hand ${_q(r['onHand'])} · level ${_q(r['reorderLevel'])}'),
                          trailing: Text('Order ${_q(r['suggestedOrder'])}'),
                        ),
                      const Divider(),
                    ],
                    TextField(decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Search stock', border: OutlineInputBorder()), onChanged: (v) => setState(() => _search = v)),
                    const SizedBox(height: ErpSpace.s2),
                    if (rows.isEmpty) const Padding(padding: EdgeInsets.all(ErpSpace.s6), child: Center(child: Text('No stock found.'))),
                    for (final r in rows) stockTile(r),
                  ]),
                ),
    );
  }
}

Widget stockTile(Json r) => ListTile(
      title: Text('${r['item']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${r['sku']} · ${r['warehouse']}\nAverage cost ${_m(r['averageCost'])}'),
      isThreeLine: true,
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text('${_q(r['quantity'])} ${r['uom'] ?? ''}', style: const TextStyle(fontWeight: FontWeight.w600)),
        Text(_m(r['value'])),
      ]),
    );

// ---------------------------------------------------------------- movements

Widget movementTile(BuildContext context, Json m) {
  final kind = (m['kind'] ?? '').toString();
  final qty = _n(m['quantity']);
  return ListTile(
    title: Text('${humanize(kind)} · ${qty > 0 ? '+' : ''}${_q(qty)}'),
    subtitle: Text([formatDate(m['date']), if ((m['reference'] ?? '').toString().isNotEmpty) m['reference'], if ((m['memo'] ?? '').toString().isNotEmpty) m['memo']].join(' · ')),
    trailing: Text('@ ${_m(m['unitCost'])}'),
  );
}

class MovementsScreen extends StatelessWidget {
  const MovementsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Stock movements',
        path: '$invPath/movements',
        searchHint: 'Search movements',
        emptyText: 'No stock movements found.',
        itemBuilder: movementTile,
        filters: movementKinds,
        filterKey: 'kind',
      );
}

// ---------------------------------------------------------------- counts

FormSpec countForm() => FormSpec(
      title: 'New stock count',
      intro: 'Starts a count sheet with the system quantity of every item held in the warehouse.',
      fields: [
        _warehouse('warehouseId', 'Warehouse'),
        Field.date('countDate', 'Count date', required: true, initial: 'today'),
        Field.text('memo', 'Memo', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$invPath/counts', body: body),
    );

FormSpec countedQuantitiesForm(Json count) => FormSpec(
      title: 'Enter counts',
      intro: 'Leave a quantity empty to clear it. Add an item that is not on the sheet to count unexpected stock.',
      initial: {
        'lines': [
          for (final l in (count['lines'] as List<dynamic>).cast<Json>())
            {'itemId': l['itemId'], '__label_itemId': '${l['sku']} ${l['item']}', 'countedQuantity': l['countedQuantity']},
        ],
      },
      fields: [
        Field.lines('lines', 'Items', minLines: 1, addLabel: 'Add item', fields: [
          _product('itemId', 'Item'),
          Field.decimal('countedQuantity', 'Counted quantity', min: 0),
        ]),
      ],
      submit: (ref, body, raw) {
        // A cleared quantity must reach the server as null, not be dropped.
        final lines = [
          for (final l in (raw['lines'] as List<dynamic>).cast<Json>()) {'itemId': l['itemId'], 'countedQuantity': toNum(l['countedQuantity'])},
        ];
        return ref.read(apiProvider).put('$invPath/counts/${count['id']}/lines', body: {'lines': lines});
      },
    );

Widget countTile(BuildContext context, Json c) {
  final status = (c['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CountDetailScreen(id: c['id'].toString()))),
    title: Text('${c['number']}'),
    subtitle: Text('${formatDate(c['countDate'])} · ${c['lines']} lines${(c['memo'] ?? '').toString().isEmpty ? '' : '\n${c['memo']}'}'),
    trailing: StatusChip(humanize(status), color: status == 'posted' ? Colors.green.shade700 : (status == 'cancelled' ? Colors.red.shade700 : Colors.blueGrey)),
  );
}

class CountsScreen extends StatelessWidget {
  const CountsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Stock counts',
        path: '$invPath/counts',
        searchHint: 'Search counts',
        emptyText: 'No stock counts found.',
        itemBuilder: countTile,
        filters: countStatuses,
        create: (ref) => countForm(),
        createPermission: 'inventory.counts.manage',
      );
}

class CountDetailScreen extends StatelessWidget {
  const CountDetailScreen({super.key, required this.id});

  final String id;

  bool _open(Json d) => d['status'] == 'draft' || d['status'] == 'counting';

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$invPath/counts/$id',
        titleOf: (d) => '${d['number']}',
        body: (context, d) {
          final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['warehouse']}', subtitle: 'Count date ${formatDate(d['countDate'])}', status: d['status']?.toString()),
            InfoCard(rows: [InfoRow('Memo', text(d['memo'])), InfoRow('Recorded', d['postedAt'] == null ? null : formatDate(d['postedAt'])), InfoRow('Lines', '${lines.length}')]),
            Text('Items', style: Theme.of(context).textTheme.titleMedium),
            for (final l in lines)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${l['item']}'),
                subtitle: Text('${l['sku']} · system ${_q(l['systemQuantity'])} · counted ${l['countedQuantity'] == null ? '-' : _q(l['countedQuantity'])}'),
                trailing: l['variance'] == null ? null : Text('${_n(l['variance']) > 0 ? '+' : ''}${_q(l['variance'])}', style: TextStyle(fontWeight: FontWeight.w600, color: _n(l['variance']) == 0 ? null : Colors.orange.shade800)),
              ),
          ]);
        },
        actions: [
          ActionSpec(label: 'Enter counts', icon: Icons.edit_note, permission: 'inventory.counts.manage', visible: _open, form: countedQuantitiesForm, run: (ref, d, _) async => null),
          ActionSpec(
            label: 'Record',
            icon: Icons.check_circle_outline,
            permission: 'inventory.counts.manage',
            visible: _open,
            confirm: 'Record this count? One stock adjustment is booked for every difference.',
            successMessage: 'Count recorded',
            run: (ref, d, _) => ref.read(apiProvider).post('$invPath/counts/$id/post'),
          ),
          ActionSpec(
            label: 'Cancel count',
            icon: Icons.block,
            permission: 'inventory.counts.manage',
            destructive: true,
            visible: _open,
            confirm: 'Cancel this count?',
            successMessage: 'Count cancelled',
            run: (ref, d, _) => ref.read(apiProvider).post('$invPath/counts/$id/cancel'),
          ),
        ],
      );
}

// ---------------------------------------------------------------- warehouses

FormSpec warehouseForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New warehouse',
    initial: {if (editing) ...{'name': existing['name'], 'address': existing['address'], 'isActive': existing['isActive']}, 'legalEntityId': existing?['legalEntityId'] ?? me.defaultEntity?.id},
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      if (!editing) Field.text('code', 'Code', required: true, maxLength: 50),
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.multiline('address', 'Address', maxLength: 300),
      if (editing) Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$invPath/warehouses/${existing['id']}', body: {'address': null, ...body}) : api.post('$invPath/warehouses', body: body);
    },
  );
}

class WarehousesScreen extends StatelessWidget {
  const WarehousesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Warehouses',
        path: '$invPath/warehouses',
        searchHint: 'Search warehouses',
        emptyText: 'No warehouses found.',
        itemBuilder: (context, w) => ListTile(
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => WarehouseDetailScreen(warehouse: w))),
          leading: const Icon(Icons.warehouse_outlined),
          title: Text('${w['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text('${w['code']}'),
          trailing: StatusChip(w['isActive'] == true ? 'Active' : 'Inactive', color: w['isActive'] == true ? Colors.green.shade700 : Colors.grey.shade700),
        ),
        create: (ref) => warehouseForm(ref),
        createPermission: 'inventory.warehouses.write',
      );
}

/// The API has no read-one endpoint for warehouses, so this shows the list row and edits it.
class WarehouseDetailScreen extends ConsumerStatefulWidget {
  const WarehouseDetailScreen({super.key, required this.warehouse});

  final Json warehouse;

  @override
  ConsumerState<WarehouseDetailScreen> createState() => _WarehouseDetailState();
}

class _WarehouseDetailState extends ConsumerState<WarehouseDetailScreen> {
  late Json _w = widget.warehouse;

  @override
  Widget build(BuildContext context) {
    final can = ref.watch(meOrEmptyProvider).can('inventory.warehouses.write');
    return Scaffold(
      appBar: AppBar(title: Text('${_w['name']}'), actions: [
        if (can)
          IconButton(
            tooltip: 'Edit',
            icon: const Icon(Icons.edit_outlined),
            onPressed: () async {
              final saved = await openForm<Object?>(context, warehouseForm(ref, existing: _w));
              if (saved is Json && mounted) setState(() => _w = {..._w, ...saved});
            },
          ),
      ]),
      body: DetailBody(children: [
        DetailHeader(title: '${_w['name']}', subtitle: '${_w['code']}', status: _w['isActive'] == true ? 'active' : 'inactive'),
        InfoCard(rows: [InfoRow('Address', text(_w['address']))]),
      ]),
    );
  }
}
