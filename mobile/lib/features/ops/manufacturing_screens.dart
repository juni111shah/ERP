import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

const mfgPath = '/api/v1/m/manufacturing';

num _n(Object? v) => toNum(v) ?? 0;
String _q(Object? v) => formatKpi(_n(v).toDouble(), 'number');
String _m(Object? v) => formatMoney(_n(v));

Color _statusColor(String s) => switch (s) {
      'completed' => Colors.green.shade700,
      'inProgress' => Colors.blue.shade700,
      'planned' => Colors.orange.shade800,
      'cancelled' => Colors.red.shade700,
      _ => Colors.blueGrey,
    };

// ---------------------------------------------------------------- BOMs

FormSpec bomForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New bill of materials',
    initial: {
      'legalEntityId': existing?['legalEntityId'] ?? me.defaultEntity?.id,
      if (editing) ...{
        'name': existing['name'],
        'outputQuantity': existing['outputQuantity'],
        'uom': existing['uom'],
        'version': existing['version'],
        'laborCostPerUnit': existing['laborCostPerUnit'],
        'overheadCostPerUnit': existing['overheadCostPerUnit'],
        'isActive': existing['isActive'],
        'description': existing['description'],
      },
    },
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      if (!editing) Field.text('code', 'Code', required: true, maxLength: 50),
      Field.text('name', 'Name', required: true, maxLength: 150),
      if (!editing) Field.lookup('producedItemId', 'Product made', path: '/api/v1/items', query: const {'filter': 'type:eq:product'}, subtitleKey: 'sku', required: true),
      Field.decimal('outputQuantity', 'Output quantity', required: true, min: 0.0001, initial: '1'),
      Field.text('uom', 'Unit of measure', maxLength: 20, initial: 'PCS'),
      Field.text('version', 'Version', maxLength: 20, initial: '1.0'),
      Field.money('laborCostPerUnit', 'Labor cost per unit', initial: '0'),
      Field.money('overheadCostPerUnit', 'Overhead cost per unit', initial: '0'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('description', 'Description', maxLength: 500),
      // Editing the components of an existing BOM answers 500 ("Cross-workspace write blocked") on the current backend, so the edit form covers the header only.
      if (!editing) Field.lines('components', 'Components', minLines: 0, addLabel: 'Add component', fields: [
        Field.lookup('componentItemId', 'Component', path: '/api/v1/items', query: const {'filter': 'type:eq:product'}, subtitleKey: 'sku', required: true, copy: const {'unitCost': 'purchasePrice'}),
        Field.decimal('quantityRequired', 'Quantity required', required: true, min: 0.0001, initial: '1'),
        Field.decimal('scrapPercentage', 'Scrap %', min: 0, max: 100, decimals: 2, initial: '0'),
        Field.money('unitCost', 'Unit cost', required: true),
        Field.text('notes', 'Notes', maxLength: 200),
      ]),
    ],
    summary: (context, raw) {
      final cost = [
        for (final c in (raw['components'] as List<dynamic>? ?? const [])) _n((c as Json)['quantityRequired']) * _n(c['unitCost']) * (1 + _n(c['scrapPercentage']) / 100),
      ].fold<num>(0, (a, b) => a + b);
      return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Material cost', formatMoney(cost), bold: true));
    },
    submit: (ref, body, raw) {
      // The server takes names and codes from the catalog, but the contract has them as required strings.
      final components = [
        for (final c in (body['components'] as List<dynamic>? ?? const []).cast<Json>()) {'componentItemCode': '', 'componentItemName': '', 'scrapPercentage': 0, 'unitCost': 0, ...c},
      ];
      final api = ref.read(apiProvider);
      final payload = {'uom': 'PCS', 'version': '1.0', 'laborCostPerUnit': 0, 'overheadCostPerUnit': 0, ...body, if (!editing) 'components': components};
      return editing
          ? api.put('$mfgPath/boms/${existing['id']}', body: payload)
          : api.post('$mfgPath/boms', body: {...payload, 'producedItemCode': '', 'producedItemName': ''});
    },
  );
}

Widget bomTile(BuildContext context, Json b) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => BomDetailScreen(id: b['id'].toString()))),
      title: Text('${b['code']} · ${b['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${b['producedItemName']}\nv${b['version']} · ${b['componentCount']} components'),
      isThreeLine: true,
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(_m(b['totalUnitCost']), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(b['isActive'] == true ? 'Active' : 'Inactive', color: b['isActive'] == true ? Colors.green.shade700 : Colors.grey.shade700),
      ]),
    );

class BomsScreen extends StatelessWidget {
  const BomsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Bills of materials',
        path: '$mfgPath/boms',
        searchHint: 'Search BOMs',
        emptyText: 'No bills of materials found.',
        itemBuilder: bomTile,
        create: (ref) => bomForm(ref),
        createPermission: 'manufacturing.boms.manage',
      );
}

class BomDetailScreen extends StatelessWidget {
  const BomDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$mfgPath/boms/$id',
          titleOf: (d) => '${d['code']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('manufacturing.boms.manage'),
          edit: (d) => bomForm(ref, existing: d),
          body: (context, d) {
            final comps = (d['components'] as List<dynamic>? ?? const []).cast<Json>();
            return DetailBody(children: [
              DetailHeader(title: '${d['name']}', subtitle: '${d['producedItemName']} · v${d['version']}', status: d['isActive'] == true ? 'active' : 'inactive'),
              InfoCard(rows: [
                InfoRow('Output', '${_q(d['outputQuantity'])} ${d['uom']}'),
                InfoRow('Material cost', _m(d['totalMaterialCost'])),
                InfoRow('Labor per unit', _m(d['laborCostPerUnit'])),
                InfoRow('Overhead per unit', _m(d['overheadCostPerUnit'])),
                InfoRow('Total unit cost', _m(d['totalUnitCost']), bold: true),
                InfoRow('Description', text(d['description'])),
              ]),
              Text('Components', style: Theme.of(context).textTheme.titleMedium),
              for (final c in comps)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('${c['componentItemName']}'),
                  subtitle: Text('${c['componentItemCode']} · ${_q(c['quantityRequired'])} ${c['uom'] ?? ''}${_n(c['scrapPercentage']) > 0 ? ' · ${_q(c['scrapPercentage'])}% scrap' : ''}'),
                  trailing: Text(_m(c['extendedCost'])),
                ),
            ]);
          },
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'manufacturing.boms.manage',
              destructive: true,
              confirm: 'Delete this BOM? A BOM used by work orders cannot be deleted; deactivate it instead.',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$mfgPath/boms/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- work orders

FormSpec workOrderForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'New work order',
    initial: {'legalEntityId': me.defaultEntity?.id},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      Field.lookup('bomId', 'Bill of materials', path: '$mfgPath/boms', subtitleKey: 'code', required: true),
      Field.decimal('plannedQuantity', 'Planned quantity', required: true, min: 0.0001),
      Field.lookup('warehouseId', 'Warehouse', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code', required: true),
      Field.date('startDate', 'Start date', required: true, initial: 'today'),
      Field.date('dueDate', 'Due date', required: true, initial: 'today', validator: (v, values) {
        final s = values['startDate']?.toString();
        return (s != null && v != null && v.toString().compareTo(s) < 0) ? 'Due date cannot be before the start date' : null;
      }),
      Field.multiline('notes', 'Notes', maxLength: 500),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$mfgPath/workorders', body: body),
  );
}

Widget workOrderTile(BuildContext context, Json w) {
  final status = (w['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => WorkOrderDetailScreen(id: w['id'].toString()))),
    title: Text('${w['workOrderNumber']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${w['producedItemName']}\n${_q(w['completedQuantity'])} / ${_q(w['plannedQuantity'])} ${w['uom']} · due ${formatDate(w['dueDate'])}'),
    isThreeLine: true,
    trailing: StatusChip(humanize(status), color: _statusColor(status)),
  );
}

class WorkOrdersScreen extends StatelessWidget {
  const WorkOrdersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Work orders',
        path: '$mfgPath/workorders',
        searchHint: 'Search work orders',
        emptyText: 'No work orders found.',
        itemBuilder: workOrderTile,
        create: (ref) => workOrderForm(ref),
        createPermission: 'manufacturing.workorders.manage',
      );
}

FormSpec completeWorkOrderForm(Json wo) => FormSpec(
      title: 'Complete ${wo['workOrderNumber']}',
      intro: 'Planned ${_q(wo['plannedQuantity'])} ${wo['uom']}. Completing receives the finished goods into stock and consumes the components.',
      submitLabel: 'Complete',
      initial: {'completedQuantity': wo['plannedQuantity'], 'scrappedQuantity': 0, 'actualTotalCost': wo['estimatedTotalCost']},
      fields: [
        Field.decimal('completedQuantity', 'Quantity produced', required: true, min: 0),
        Field.decimal('scrappedQuantity', 'Quantity scrapped', min: 0, initial: '0'),
        Field.money('actualTotalCost', 'Actual total cost', required: true),
        Field.multiline('notes', 'Notes', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$mfgPath/workorders/${wo['id']}/complete', body: {'scrappedQuantity': 0, ...body}),
    );

class WorkOrderDetailScreen extends StatelessWidget {
  const WorkOrderDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$mfgPath/workorders/$id',
        titleOf: (d) => '${d['workOrderNumber']}',
        body: (context, d) {
          final ops = (d['operations'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['producedItemName']}', subtitle: '${d['producedItemCode']} · BOM ${d['bomCode']}', status: d['status']?.toString()),
            InfoCard(rows: [
              InfoRow('Planned', '${_q(d['plannedQuantity'])} ${d['uom']}'),
              InfoRow('Completed', _q(d['completedQuantity'])),
              InfoRow('Scrapped', _q(d['scrappedQuantity'])),
              InfoRow('Start', formatDate(d['startDate'])),
              InfoRow('Due', formatDate(d['dueDate'])),
              InfoRow('Completed on', d['completionDate'] == null ? null : formatDate(d['completionDate'])),
              InfoRow('Estimated cost', _m(d['estimatedTotalCost'])),
              InfoRow('Actual cost', _m(d['actualTotalCost']), bold: true),
              InfoRow('Notes', text(d['notes'])),
            ]),
            if (ops.isNotEmpty) ...[
              Text('Operations', style: Theme.of(context).textTheme.titleMedium),
              for (final o in ops)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('${o['sequenceOrder']}. ${o['operationName']}'),
                  subtitle: Text('${o['workCenterName']} · ${_q(o['plannedHours'])} h planned'),
                  trailing: Text(humanize('${o['status']}')),
                ),
            ],
          ]);
        },
        actions: [
          ActionSpec(
            label: 'Release',
            icon: Icons.play_circle_outline,
            permission: 'manufacturing.workorders.manage',
            visible: (d) => d['status'] == 'planned',
            confirm: 'Release this work order to production?',
            successMessage: 'Work order released',
            run: (ref, d, _) => ref.read(apiProvider).post('$mfgPath/workorders/$id/release'),
          ),
          ActionSpec(label: 'Complete', icon: Icons.task_alt, permission: 'manufacturing.workorders.manage', visible: (d) => d['status'] == 'inProgress', form: completeWorkOrderForm, run: (ref, d, _) async => null),
          ActionSpec(
            label: 'Cancel',
            icon: Icons.block,
            permission: 'manufacturing.workorders.manage',
            destructive: true,
            visible: (d) => d['status'] == 'draft' || d['status'] == 'planned' || d['status'] == 'inProgress',
            reasonLabel: 'Why is this work order being cancelled?',
            successMessage: 'Work order cancelled',
            run: (ref, d, reason) => ref.read(apiProvider).post('$mfgPath/workorders/$id/cancel', body: {'reason': reason}),
          ),
        ],
      );
}

// ---------------------------------------------------------------- costing and analytics

/// Product costing: pick a BOM, see its standard cost rollup.
class CostingScreen extends StatelessWidget {
  const CostingScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Product costing',
        path: '$mfgPath/boms',
        searchHint: 'Search BOMs',
        emptyText: 'No bills of materials found.',
        itemBuilder: (context, b) => ListTile(
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CostRollupScreen(bomId: b['id'].toString()))),
          title: Text('${b['producedItemName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text('${b['code']} · v${b['version']}'),
          trailing: Text(_m(b['totalUnitCost']), style: const TextStyle(fontWeight: FontWeight.w600)),
        ),
      );
}

class CostRollupScreen extends StatelessWidget {
  const CostRollupScreen({super.key, required this.bomId});

  final String bomId;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$mfgPath/costing/rollup/$bomId',
        titleOf: (d) => '${d['producedItemCode']}',
        body: (context, d) {
          final parts = (d['componentBreakdown'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['producedItemName']}', subtitle: 'Output ${_q(d['outputQuantity'])}'),
            InfoCard(rows: [
              InfoRow('Material', _m(d['materialCost'])),
              InfoRow('Labor', _m(d['laborCost'])),
              InfoRow('Overhead', _m(d['overheadCost'])),
              InfoRow('Standard unit cost', _m(d['totalStandardUnitCost']), bold: true),
            ]),
            Text('Components', style: Theme.of(context).textTheme.titleMedium),
            for (final c in parts)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${c['componentItemName']}'),
                subtitle: Text('${c['componentItemCode']} · ${_q(c['quantityRequired'])} × ${_m(c['unitCost'])}'),
                trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [Text(_m(c['totalCost'])), Text('${_q(c['costSharePercentage'])}%')]),
              ),
          ]);
        },
      );
}

class MfgAnalyticsScreen extends ConsumerWidget {
  const MfgAnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get('$mfgPath/kpis'),
        builder: (context, snap) => Scaffold(
          appBar: AppBar(title: const Text('Manufacturing analytics')),
          body: snap.connectionState != ConnectionState.done
              ? const Center(child: CircularProgressIndicator())
              : snap.hasError
                  ? ErrorView(message: snap.error is ApiException ? (snap.error! as ApiException).message : '${snap.error}', onRetry: () {})
                  : _kpis(snap.data as Json),
        ),
      );

  Widget _kpis(Json k) => DetailBody(children: [
        InfoCard(title: 'Production', rows: [
          InfoRow('Active BOMs', '${k['activeBomsCount']}'),
          InfoRow('Open work orders', '${k['openWorkOrdersCount']}'),
          InfoRow('Completed this month', '${k['completedWorkOrdersMonth']}'),
          InfoRow('Produced this month', _q(k['totalProducedQuantityMonth'])),
          InfoRow('Average yield', '${_q(k['averageYieldRatePercentage'])}%'),
          InfoRow('Manufacturing cost this month', _m(k['totalManufacturingCostMonth']), bold: true),
        ]),
      ]);
}
