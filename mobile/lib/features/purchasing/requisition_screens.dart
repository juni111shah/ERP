import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import 'order_screens.dart';
import 'purchasing_common.dart';
import 'package:erp_mobile/theme/tokens.dart';

const requisitionsPath = '$purchasingPath/requisitions';

String requisitionStatusLabel(String s) => humanize(s);

FormSpec requisitionForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entityId = existing?['legalEntityId'] ?? me.defaultEntity?.id;
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New requisition',
    initial: {
      'legalEntityId': entityId,
      'requesterName': existing?['requesterName'] ?? me.fullName,
      if (editing) ...{
        'department': existing['department'],
        'requisitionDate': existing['requisitionDate'],
        'requiredDate': existing['requiredDate'],
        'reason': existing['reason'],
        'lines': [
          for (final l in (existing['lines'] as List<dynamic>))
            {
              'itemId': (l as Json)['itemId'],
              if (l['itemId'] != null) '__label_itemId': l['itemCode'] ?? 'Selected item',
              'itemCode': l['itemCode'],
              'description': l['description'],
              'quantity': l['quantity'],
              'unitOfMeasure': l['unitOfMeasure'],
              'estimatedUnitPrice': l['estimatedUnitPrice'],
            },
        ],
      },
    },
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entityId, visibleWhen: (_) => !editing),
      Field.text('requesterName', 'Requested by', required: true, maxLength: 150),
      Field.text('department', 'Department', maxLength: 100),
      Field.date('requisitionDate', 'Date', required: true, initial: 'today'),
      Field.date('requiredDate', 'Needed by'),
      Field.lines('lines', 'Lines', fields: [
        Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'description': 'name', 'itemCode': 'sku', 'estimatedUnitPrice': 'purchasePrice', 'unitOfMeasure': 'uom'}),
        Field.text('description', 'Description', required: true, maxLength: 500),
        Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
        Field.text('unitOfMeasure', 'Unit', initial: 'EA', maxLength: 20),
        Field.money('estimatedUnitPrice', 'Estimated unit price', required: true),
      ]),
      Field.multiline('reason', 'Reason', maxLength: 2000),
    ],
    summary: (context, raw) {
      final total = [for (final l in (raw['lines'] as List<dynamic>)) (toNum((l as Json)['quantity']) ?? 0) * (toNum(l['estimatedUnitPrice']) ?? 0)].fold<num>(0, (a, b) => a + b);
      return Padding(padding: const EdgeInsets.only(top: ErpSpace.s1), child: InfoRow('Estimated total', formatMoney(total), bold: true));
    },
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {
        ...body,
        'legalEntityId': body['legalEntityId'] ?? entityId,
        'lines': withLineDefaults(body['lines'] as List<dynamic>, raw['lines'] as List<dynamic>),
      };
      return editing ? api.put('$requisitionsPath/${existing['id']}', body: full) : api.post(requisitionsPath, body: full);
    },
  );
}

/// Choose the vendor for the order a requisition becomes.
FormSpec convertForm(Json requisition) => FormSpec(
      title: 'Convert ${requisition['number']}',
      intro: 'A draft purchase order is created from the lines of this requisition.',
      submitLabel: 'Create order',
      fields: [Field.lookup('vendorPartyId', 'Vendor', path: '/api/v1/parties', query: const {'role': 'Vendor'}, subtitleKey: 'code', required: true)],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$requisitionsPath/${requisition['id']}/convert-to-order', body: {
            'vendorPartyId': body['vendorPartyId'],
            'vendorName': raw['__label_vendorPartyId'] ?? '',
          }),
    );

Widget requisitionTile(BuildContext context, Json r) {
  final status = (r['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => RequisitionDetailScreen(id: r['id'].toString()))),
    title: Text('${r['number']} · ${r['requesterName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${r['department'] ?? ''}${(r['department'] ?? '').toString().isEmpty ? '' : ' · '}${formatDate(r['requisitionDate'])}'),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(formatMoney((r['estimatedTotal'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      purchasingStatusChip(status, requisitionStatusLabel(status)),
    ]),
  );
}

class RequisitionsScreen extends StatelessWidget {
  const RequisitionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Requisitions',
        path: requisitionsPath,
        searchHint: 'Search requisitions',
        emptyText: 'No requisitions found.',
        itemBuilder: requisitionTile,
        filters: requisitionStatuses,
        create: (ref) => requisitionForm(ref),
        createPermission: 'purchasing.requisitions.manage',
      );
}

ActionSpec requisitionStatusAction(String path, String label, IconData icon, String target, Set<String> from, {bool destructive = false, String? confirm}) => ActionSpec(
      label: label,
      icon: icon,
      permission: 'purchasing.requisitions.manage',
      destructive: destructive,
      visible: (d) => from.contains(d['status']),
      confirm: confirm,
      successMessage: '$label done',
      run: (ref, d, _) => ref.read(apiProvider).put('$path/status', body: {'status': target}),
    );

class RequisitionDetailScreen extends StatelessWidget {
  const RequisitionDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$requisitionsPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['number']}',
          canEdit: (d) => d['status'] != 'ordered' && ref.read(meOrEmptyProvider).can('purchasing.requisitions.manage'),
          edit: (d) => requisitionForm(ref, existing: d),
          body: (context, d) => requisitionBody(context, d),
          actions: [
            requisitionStatusAction(path, 'Submit', Icons.send_outlined, 'pendingApproval', {'draft'}, confirm: 'Send this requisition for approval?'),
            requisitionStatusAction(path, 'Approve', Icons.check_circle_outline, 'approved', {'draft', 'pendingApproval'}, confirm: 'Approve this requisition?'),
            requisitionStatusAction(path, 'Reject', Icons.cancel_outlined, 'rejected', {'draft', 'pendingApproval'}, destructive: true, confirm: 'Reject this requisition?'),
            requisitionStatusAction(path, 'Back to draft', Icons.undo, 'draft', {'rejected'}),
            ActionSpec(
              label: 'Convert to order',
              icon: Icons.shopping_cart_checkout,
              permission: 'purchasing.orders.manage',
              visible: (d) => d['status'] == 'approved',
              form: convertForm,
              run: (ref, d, _) async => null,
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'purchasing.requisitions.manage',
              destructive: true,
              visible: (d) => d['status'] != 'ordered',
              confirm: 'Delete this requisition?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete(path);
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

Widget requisitionBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  final status = '${d['status']}';
  return DetailBody(children: [
    DetailHeader(title: d['requesterName']?.toString() ?? '', subtitle: '${d['department'] ?? ''}'.isEmpty ? formatDate(d['requisitionDate']) : '${d['department']} · ${formatDate(d['requisitionDate'])}'),
    Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: purchasingStatusChip(status, requisitionStatusLabel(status)))),
    InfoCard(rows: [
      InfoRow('Estimated total', formatMoney((d['estimatedTotal'] as num?) ?? 0), bold: true),
      InfoRow('Needed by', d['requiredDate'] == null ? null : formatDate(d['requiredDate'])),
      InfoRow('Reason', text(d['reason'])),
      InfoRow('Purchase order', d['convertedPurchaseOrderId'] != null ? 'Created' : null),
    ]),
    if (d['convertedPurchaseOrderId'] != null)
      Padding(
        padding: const EdgeInsets.only(bottom: ErpSpace.s3),
        child: OutlinedButton.icon(
          onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(id: '${d['convertedPurchaseOrderId']}'))),
          icon: const Icon(Icons.open_in_new),
          label: const Text('Open purchase order'),
        ),
      ),
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} ${l['unitOfMeasure'] ?? ''} × ${formatMoney((l['estimatedUnitPrice'] as num?) ?? 0)}'),
        trailing: Text(formatMoney((l['lineTotal'] as num?) ?? 0)),
      ),
  ]);
}
