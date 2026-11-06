import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/field_view.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/state_view.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';
import 'pos_cart.dart';

const posPath = '/api/v1/m/pos';

num _n(Object? v) => toNum(v) ?? 0;
String _m(Object? v) => formatMoney(_n(v));
String _q(Object? v) => formatKpi(_n(v).toDouble(), 'number');
String _tender(Object? method) => tenderLabels['$method'] ?? humanize('$method');

/// A number to edit: every decimal it has, so opening a line and pressing Apply changes nothing.
String _plain(double v) => v == v.truncateToDouble() ? v.toInt().toString() : v.toString();

void _say(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message), duration: const Duration(seconds: 5)));
}

final _secureRandom = math.Random.secure();

/// One key per ticket or return, sent as the Idempotency-Key header: a retry after a dropped answer gets the first answer back and charges nothing twice.
String newIdempotencyKey() => [for (var i = 0; i < 16; i++) _secureRandom.nextInt(256).toRadixString(16).padLeft(2, '0')].join();

const orderStatuses = [Option('completed', 'Completed'), Option('voided', 'Voided'), Option('parked', 'Parked')];
const sessionStatuses = [Option('open', 'Open'), Option('closed', 'Closed')];

// ---------------------------------------------------------------- registers

const _accountsPath = '/api/v1/finance/accounts';

/// On the server these three keep their value when left out, so clearing one sends the empty id.
const _keptWhenOmitted = ['cardAccountId', 'varianceAccountId', 'expenseAccountId'];
const _emptyId = '00000000-0000-0000-0000-000000000000';

/// The update replaces the whole record: send what the form does not edit, and say so when a choice was cleared.
Map<String, dynamic> registerUpdateBody(Json existing, Values body) => {
      'warehouseId': null,
      'defaultCashAccountId': null,
      'customFields': existing['customFields'],
      for (final key in _keptWhenOmitted) key: _emptyId,
      ...body,
    };

FormSpec registerForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New register',
    intro: 'Sales take stock from the warehouse. Left empty, the warehouse is the first of the legal entity, cash goes to cash on hand, card takings to the bank account, and cash over, short and pay-outs to general expenses.',
    initial: {
      if (editing) ...{
        'name': existing['name'],
        'isActive': existing['isActive'],
        'receiptHeader': existing['receiptHeader'],
        'receiptFooter': existing['receiptFooter'],
        'warehouseId': existing['warehouseId'],
        if (existing['warehouseId'] != null) '__label_warehouseId': 'Selected warehouse',
        for (final key in ['defaultCashAccountId', ..._keptWhenOmitted]) ...{
          key: existing[key],
          if (existing[key] != null) '__label_$key': 'Selected account',
        },
      },
      'legalEntityId': existing?['legalEntityId'] ?? me.defaultEntity?.id,
    },
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      if (!editing) Field.text('code', 'Code', required: true, maxLength: 50),
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.lookup('warehouseId', 'Warehouse', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code'),
      Field.lookup('defaultCashAccountId', 'Cash account', path: _accountsPath, query: const {'filter': 'type:eq:asset'}, subtitleKey: 'code'),
      Field.lookup('cardAccountId', 'Card takings account', path: _accountsPath, query: const {'filter': 'type:eq:asset'}, subtitleKey: 'code'),
      Field.lookup('varianceAccountId', 'Cash over and short account', path: _accountsPath, query: const {'filter': 'isActive:eq:true'}, subtitleKey: 'code'),
      Field.lookup('expenseAccountId', 'Pay-out account', path: _accountsPath, query: const {'filter': 'isActive:eq:true'}, subtitleKey: 'code'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('receiptHeader', 'Receipt header', maxLength: 500),
      Field.multiline('receiptFooter', 'Receipt footer', maxLength: 500),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      if (editing) return api.put('$posPath/registers/${existing['id']}', body: registerUpdateBody(existing, body));
      return api.post('$posPath/registers', body: body);
    },
  );
}

class RegistersScreen extends StatelessWidget {
  const RegistersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Registers',
        path: '$posPath/registers',
        searchHint: 'Search registers',
        emptyText: 'No registers found.',
        itemBuilder: (context, r) => ListTile(
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => RegisterDetailScreen(id: r['id'].toString()))),
          leading: const Icon(Icons.point_of_sale),
          title: Text('${r['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text('${r['code']}'),
          trailing: StatusChip(r['isActive'] == true ? 'Active' : 'Inactive', color: r['isActive'] == true ? Colors.green.shade700 : Colors.grey.shade700),
        ),
        create: (ref) => registerForm(ref),
        createPermission: 'pos.registers.manage',
      );
}

class RegisterDetailScreen extends StatelessWidget {
  const RegisterDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        String chosen(Object? v) => v == null ? 'Default' : 'Chosen';
        return DetailPage(
          path: '$posPath/registers/$id',
          titleOf: (d) => '${d['name']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('pos.registers.manage'),
          edit: (d) => registerForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['name']}', subtitle: '${d['code']}', status: d['isActive'] == true ? 'active' : 'inactive'),
            InfoCard(rows: [
              InfoRow('Warehouse', chosen(d['warehouseId'])),
              InfoRow('Cash account', chosen(d['defaultCashAccountId'])),
              InfoRow('Card takings account', chosen(d['cardAccountId'])),
              InfoRow('Cash over and short', chosen(d['varianceAccountId'])),
              InfoRow('Pay-out account', chosen(d['expenseAccountId'])),
              InfoRow('Receipt header', text(d['receiptHeader'])),
              InfoRow('Receipt footer', text(d['receiptFooter'])),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'pos.registers.manage',
              destructive: true,
              confirm: 'Delete this register? A register with an open shift cannot be deleted.',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$posPath/registers/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- sessions (shifts)

/// With pos.enforceFloatAtOpen on, the float is counted, not accepted as a pre-filled zero.
FormSpec openShiftForm(Json register, {bool enforceFloat = true}) => FormSpec(
      title: 'Open shift',
      intro: 'Register ${register['name']}. The cashier is you.${enforceFloat ? ' Count the drawer and enter what is there, even if that is 0.' : ''}',
      submitLabel: 'Open shift',
      fields: [
        Field.money('openingCash', 'Opening cash in drawer', required: true, initial: enforceFloat ? null : '0'),
        Field.multiline('notes', 'Notes', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$posPath/sessions/open', body: {...body, 'registerId': register['id'], 'legalEntityId': register['legalEntityId']}),
    );

FormSpec closeShiftForm(Json session, {num heldCount = 0}) => FormSpec(
      title: 'Close shift',
      intro: 'Expected cash in the drawer: ${_m(session['expectedCash'])}. Count the drawer and enter what is there.'
          '${heldCount > 0 ? ' $heldCount held ${heldCount == 1 ? 'sale stays' : 'sales stay'} on hold. Nothing was charged for them.' : ''}',
      submitLabel: 'Close shift',
      fields: [
        Field.money('actualCash', 'Counted cash', required: true),
        Field.multiline('notes', 'Notes', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$posPath/sessions/${session['id']}/close', body: body),
    );

FormSpec cashMovementForm(Json session) => FormSpec(
      title: 'Cash in / out',
      fields: [
        Field.select('type', 'Movement', options: const [Option('payIn', 'Cash in (pay in)'), Option('payOut', 'Cash out (pay out)'), Option('dropOut', 'Drop to safe')], required: true, initial: 'payIn'),
        Field.money('amount', 'Amount', required: true, min: 0.01),
        Field.text('reason', 'Reason', required: true, maxLength: 200),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$posPath/sessions/${session['id']}/drawer-transaction', body: body),
    );

Widget sessionTile(BuildContext context, Json s) {
  final status = (s['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => SessionDetailScreen(row: s))),
    title: Text('${s['registerName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${s['cashierName']}\nOpened ${formatDate(s['openedAt'])}'),
    isThreeLine: true,
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(_m(s['totalSales']), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      StatusChip(humanize(status), color: status == 'open' ? Colors.green.shade700 : Colors.blueGrey),
    ]),
  );
}

class SessionsScreen extends StatelessWidget {
  const SessionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Shifts & drawers',
        path: '$posPath/sessions',
        searchHint: 'Search shifts',
        emptyText: 'No shifts found.',
        itemBuilder: sessionTile,
        filters: sessionStatuses,
      );
}

/// An amount taken off, with no minus sign on nothing.
String _less(Object? v) => _n(v) == 0 ? _m(v) : '-${_m(v)}';

const _drawerMoves = {'floatIn': 'Float in', 'payIn': 'Cash in', 'payOut': 'Cash out', 'dropOut': 'Safe drop'};

/// A shift on paper: the X report while it is open, the Z report once closed. `GET sessions/{id}/report`.
Widget shiftReportBody(BuildContext context, Json r) {
  final s = r['session'] as Json;
  final closed = s['status'] == 'closed';
  final tenders = (r['tenders'] as List<dynamic>? ?? const []).cast<Json>();
  final top = (r['topItems'] as List<dynamic>? ?? const []).cast<Json>();
  final moves = (s['drawerTransactions'] as List<dynamic>? ?? const []).cast<Json>().where((t) => t['reason'] != 'Opening cash float').toList();
  final difference = _n(s['cashDifference']);
  final held = _n(r['heldCount']);
  return DetailBody(children: [
    DetailHeader(
      title: closed ? 'Z report' : 'X report',
      subtitle: '${s['registerName']} · ${s['cashierName']}\n${formatDate(s['openedAt'])}${closed ? ' to ${formatDate(s['closedAt'])}' : ' (still open)'}',
      status: s['status']?.toString(),
    ),
    InfoCard(title: 'Sales', rows: [
      InfoRow('Gross sales', _m(r['grossSales'])),
      InfoRow('Discounts', _less(r['discounts'])),
      InfoRow('Net sales', _m(r['netSales'])),
      InfoRow('Tax', _m(r['tax'])),
      InfoRow('Sales total (${r['saleCount']})', _m(r['salesTotal']), bold: true),
      InfoRow('Returns (${r['returnCount']})', _less(r['returns'])),
      InfoRow('Net takings', _m(r['netTakings']), bold: true),
      InfoRow('Voided, not counted above (${r['voidCount']})', _m(r['voids'])),
      InfoRow('Items sold', _q(r['itemsSold'])),
      InfoRow('Average ticket', _m(r['averageTicket'])),
    ]),
    if (tenders.isNotEmpty)
      InfoCard(title: 'By payment', rows: [
        for (final t in tenders) InfoRow(_tender(t['method']), _n(t['refunds']) > 0 ? '${_m(t['net'])} (${_m(t['sales'])} taken, ${_m(t['refunds'])} refunded)' : _m(t['net'])),
      ]),
    InfoCard(title: 'Cash drawer', rows: [
      InfoRow('Opening float', _m(s['openingCash'])),
      InfoRow('Cash sales', _m(r['cashSales'])),
      if (_n(r['cashRefunds']) != 0) InfoRow('Cash refunds', '-${_m(r['cashRefunds'])}'),
      if (_n(s['totalPayIn']) != 0) InfoRow('Cash added', _m(s['totalPayIn'])),
      if (_n(s['totalPayOut']) != 0) InfoRow('Cash taken out', '-${_m(s['totalPayOut'])}'),
      InfoRow('Expected in drawer', _m(s['expectedCash']), bold: true),
      if (closed) ...[
        InfoRow('Counted', _m(s['actualCash'])),
        InfoRow(difference < 0 ? 'Short' : (difference > 0 ? 'Over' : 'Difference'), _m(difference), bold: true),
      ],
    ]),
    if (moves.isNotEmpty)
      InfoCard(title: 'Drawer movements', rows: [
        for (final t in moves) InfoRow('${_drawerMoves['${t['type']}'] ?? humanize('${t['type']}')}: ${t['reason']}', '${t['type'] == 'payOut' || t['type'] == 'dropOut' ? '-' : ''}${_m(t['amount'])}'),
      ]),
    if (top.isNotEmpty)
      InfoCard(title: 'Top items', rows: [
        for (final i in top) InfoRow('${i['description']}', '${_q(i['quantity'])} × · ${_m(i['revenue'])}'),
      ]),
    if (held > 0) Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: Text('$held held ${held == 1 ? 'sale stays' : 'sales stay'} on hold on this register. Nothing has been charged for them.')),
    if (s['notes'] != null) Text('${s['notes']}'),
  ]);
}

/// A shift, open or closed: its report, and cash in/out and closing while it is open. Closing reloads the same page into the Z report.
class SessionDetailScreen extends StatelessWidget {
  const SessionDetailScreen({super.key, required this.row});

  final Json row;

  static bool _open(Json report) => (report['session'] as Json)['status'] == 'open';

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$posPath/sessions/${row['id']}/report',
        titleOf: (d) => '${(d['session'] as Json)['registerName']}',
        body: shiftReportBody,
        actions: [
          ActionSpec(label: 'Cash in/out', icon: Icons.swap_vert, permission: 'pos.sessions.manage', visible: _open, form: (d) => cashMovementForm(d['session'] as Json), run: (ref, d, _) async => null),
          ActionSpec(
            label: 'Close shift',
            icon: Icons.lock_outline,
            permission: 'pos.sessions.manage',
            destructive: true,
            visible: _open,
            form: (d) => closeShiftForm(d['session'] as Json, heldCount: _n(d['heldCount'])),
            run: (ref, d, _) async => null,
          ),
        ],
      );
}

// ---------------------------------------------------------------- orders

Widget orderTile(BuildContext context, Json o) {
  final status = (o['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(id: o['id'].toString()))),
    title: Text('${o['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${o['customerName'] ?? ''}\n${formatDate(o['orderDate'])} · ${o['lineCount']} item lines'),
    isThreeLine: true,
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(_m(o['totalAmount']), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      StatusChip(humanize(status), color: StatusChip.forStatus(status == 'completed' ? 'paid' : status)),
    ]),
  );
}

class OrdersScreen extends StatelessWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Counter orders',
        path: '$posPath/orders',
        searchHint: 'Search orders',
        emptyText: 'No counter orders found.',
        itemBuilder: orderTile,
        filters: orderStatuses,
      );
}

bool _canReturn(Json order) => order['status'] == 'completed' && (order['lines'] as List<dynamic>? ?? const []).any((l) => _n((l as Json)['returnedQuantity']) < _n(l['quantity']));

/// The shift that gives the money back: on the register that made the sale if it has one open, else any open shift of the same legal entity.
Future<Json?> _openShiftFor(WidgetRef ref, Json order) async {
  final terminal = await ref.read(apiProvider).get('$posPath/terminal') as Json;
  final open = (terminal['registers'] as List<dynamic>).cast<Json>().where((r) => r['openSessionId'] != null && r['legalEntityId'] == order['legalEntityId']).toList();
  if (open.isEmpty) return null;
  final register = open.where((r) => r['id'] == order['registerId']).firstOrNull ?? open.first;
  return {'id': register['openSessionId'], 'registerName': register['name']};
}

class OrderDetailScreen extends ConsumerWidget {
  const OrderDetailScreen({super.key, required this.id});

  final String id;

  Future<void> _startReturn(BuildContext context, WidgetRef ref, Json order) async {
    try {
      final shift = await _openShiftFor(ref, order);
      if (!context.mounted) return;
      if (shift == null) {
        _say(context, 'Open a shift to give money back.');
        return;
      }
      await Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PosReturnScreen(session: shift, initialNumber: '${order['number']}')));
    } on ApiException catch (e) {
      if (context.mounted) _say(context, e.message);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) => DetailPage(
        path: '$posPath/orders/$id',
        titleOf: (d) => '${d['number']}',
        body: orderBody,
        headerActions: (context, d) => [
          if (ref.read(meOrEmptyProvider).can('pos.orders.refund') && _canReturn(d))
            IconButton(key: const ValueKey('pos-order-return'), tooltip: 'Return', icon: const Icon(Icons.assignment_return_outlined), onPressed: () => _startReturn(context, ref, d)),
        ],
        actions: [
          ActionSpec(
            label: 'Void',
            icon: Icons.block,
            permission: 'pos.orders.void',
            destructive: true,
            visible: (d) => d['status'] == 'completed' && _n(d['returnedTotal']) == 0, // the server refuses a sale part returned: return the rest instead
            reasonLabel: 'Why is this sale being voided?',
            successMessage: 'Order voided',
            run: (ref, d, reason) => ref.read(apiProvider).post('$posPath/orders/$id/void', body: {'reason': reason}),
          ),
        ],
      );
}

Widget orderBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  final payments = (d['payments'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: '${d['customerName'] ?? ''}', subtitle: '${formatDate(d['orderDate'])} · cashier ${d['cashierName']}', status: d['status']?.toString()),
    InfoCard(rows: [
      InfoRow('Subtotal', _m(d['subTotal'])),
      InfoRow('Discount', _m(d['discountTotal'])),
      InfoRow('Tax', _m(d['taxTotal'])),
      InfoRow('Total', _m(d['totalAmount']), bold: true),
      if (_n(d['returnedTotal']) > 0) InfoRow('Returned so far', _m(d['returnedTotal'])),
      InfoRow('Payment', humanize('${d['paymentStatus']}')),
      InfoRow('Invoice', text(d['invoiceNumber'])),
      InfoRow('Notes', text(d['notes'])),
    ]),
    Text('Items', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${formatMoney(_n(l['quantity']))} × ${_m(l['unitPrice'])}${_n(l['discountPercent']) > 0 ? ' · ${_n(l['discountPercent'])}% off' : ''}${_n(l['returnedQuantity']) > 0 ? ' · ${_q(l['returnedQuantity'])} returned' : ''}'),
        trailing: Text(_m(l['lineTotal'])),
      ),
    if (payments.isNotEmpty) ...[
      const SizedBox(height: ErpSpace.s2),
      Text('Payments', style: Theme.of(context).textTheme.titleMedium),
      for (final p in payments)
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(_tender(p['method'])),
          subtitle: _n(p['changeAmount']) > 0 ? Text('Tendered ${_m(p['tenderedAmount'])} · change ${_m(p['changeAmount'])}') : null,
          trailing: Text(_m(p['amount'])),
        ),
    ],
  ]);
}

// ---------------------------------------------------------------- returns

Widget returnTile(BuildContext context, Json r) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ReturnDetailScreen(id: r['id'].toString()))),
      title: Text('${r['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${r['customerName'] ?? ''} · of ${r['originalNumber']}\n${formatDate(r['returnDate'])} · ${r['reason']}', maxLines: 2, overflow: TextOverflow.ellipsis),
      isThreeLine: true,
      trailing: Text(_m(r['totalAmount']), style: const TextStyle(fontWeight: FontWeight.w600)),
    );

class ReturnsScreen extends StatelessWidget {
  const ReturnsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Returns',
        path: '$posPath/returns',
        searchHint: 'Search returns',
        emptyText: 'No returns yet.',
        itemBuilder: returnTile,
      );
}

class ReturnDetailScreen extends StatelessWidget {
  const ReturnDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$posPath/returns/$id',
        titleOf: (d) => '${d['number']}',
        body: (context, d) {
          final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
          final refunds = (d['refunds'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['customerName'] ?? ''}', subtitle: '${formatDate(d['returnDate'])} · of ${d['originalNumber']} · cashier ${d['cashierName']}'),
            InfoCard(rows: [
              InfoRow('Subtotal', _m(d['subTotal'])),
              InfoRow('Tax', _m(d['taxTotal'])),
              InfoRow('Total', _m(d['totalAmount']), bold: true),
              InfoRow('Reason', text(d['reason'])),
              InfoRow('Credit note', text(d['creditNoteNumber'])),
            ]),
            Text('Items', style: Theme.of(context).textTheme.titleMedium),
            for (final l in lines)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${l['description']}'),
                subtitle: Text('${_q(l['quantity'])} × ${_m(l['unitPrice'])} · ${l['restock'] == true ? 'back on shelf' : 'not restocked'}'),
                trailing: Text(_m(l['lineTotal'])),
              ),
            const SizedBox(height: ErpSpace.s2),
            Text('Refunds', style: Theme.of(context).textTheme.titleMedium),
            for (final f in refunds)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(_tender(f['method'])),
                subtitle: f['reference'] == null ? null : Text('${f['reference']}'),
                trailing: Text(_m(f['amount'])),
              ),
          ]);
        },
      );
}

class _ReturnRow {
  _ReturnRow(this.line)
      : left = roundQty(_n(line['quantity']) - _n(line['returnedQuantity'])),
        restock = line['itemId'] != null;

  final Json line;
  final double left;
  double quantity = 0;
  bool restock;
}

/// The body of `POST returns`: the chosen lines, and the refunds that must add up to exactly what those lines give back.
Map<String, dynamic> returnBody({required String orderId, required String sessionId, required String reason, required List<Map<String, dynamic>> lines, required List<Map<String, dynamic>> refunds}) =>
    {'orderId': orderId, 'sessionId': sessionId, 'reason': reason, 'lines': lines, 'refunds': refunds};

/// A customer brings goods back: find the sale by its receipt, choose what came back, give the money back. [session] is the open shift paying it out.
class PosReturnScreen extends ConsumerStatefulWidget {
  const PosReturnScreen({super.key, required this.session, this.initialNumber});

  final Json session;
  final String? initialNumber;

  @override
  ConsumerState<PosReturnScreen> createState() => _PosReturnState();
}

class _PosReturnState extends ConsumerState<PosReturnScreen> {
  late final _number = TextEditingController(text: widget.initialNumber ?? '');
  final _reason = TextEditingController();
  final _reference = TextEditingController();
  final _idempotencyKey = newIdempotencyKey();

  Json? _order;
  List<_ReturnRow> _rows = [];
  String _method = 'cash';
  bool _finding = false;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    if (widget.initialNumber != null) WidgetsBinding.instance.addPostFrameCallback((_) => _find());
  }

  @override
  void dispose() {
    _number.dispose();
    _reason.dispose();
    _reference.dispose();
    super.dispose();
  }

  Future<void> _find() async {
    final number = _number.text.trim();
    if (number.isEmpty) return;
    setState(() {
      _finding = true;
      _error = null;
    });
    try {
      final order = await ref.read(apiProvider).get('$posPath/orders/by-number/${Uri.encodeComponent(number)}') as Json;
      if (!mounted) return;
      setState(() {
        _order = order;
        _rows = [for (final l in (order['lines'] as List<dynamic>).cast<Json>()) _ReturnRow(l)];
        _method = defaultRefundMethod(order);
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _order = null;
          _rows = [];
          _error = e.message;
        });
      }
    } finally {
      if (mounted) setState(() => _finding = false);
    }
  }

  double get _total => round2(_rows.fold<double>(0, (sum, r) => sum + returnAmount(r.line, r.quantity)));

  List<Map<String, dynamic>> get _refunds {
    final o = _order;
    if (o == null || _total <= 0) return const [];
    final reference = _reference.text.trim();
    return splitRefund(_total, _method, _n(o['moneyLeftToRefund']).toDouble(), o['invoiceId'] != null ? _n(o['onAccountLeft']).toDouble() : 0, moneyRefundMethod(o), reference.isEmpty ? null : reference);
  }

  bool get _canSubmit => !_busy && _total > 0 && _reason.text.trim().isNotEmpty && _order?['status'] == 'completed';

  Future<void> _submit() async {
    final order = _order;
    if (order == null || !_canSubmit) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final done = await ref.read(apiProvider).post(
            '$posPath/returns',
            headers: {'Idempotency-Key': _idempotencyKey},
            body: returnBody(
              orderId: '${order['id']}',
              sessionId: '${widget.session['id']}',
              reason: _reason.text.trim(),
              lines: [
                for (final r in _rows.where((r) => r.quantity > 0)) {'orderLineId': r.line['id'], 'quantity': r.quantity, 'restock': r.restock},
              ],
              refunds: _refunds,
            ),
          ) as Json;
      ref.read(dataVersionProvider.notifier).bump();
      if (!mounted) return;
      await _showDone(done);
      if (mounted) Navigator.of(context).pop(done);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _showDone(Json done) => showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text('Refund ${done['number']}'),
          content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            for (final f in (done['refunds'] as List<dynamic>).cast<Json>()) InfoRow(_tender(f['method']), _m(f['amount'])),
            InfoRow('Total given back', _m(done['totalAmount']), bold: true),
            if (done['creditNoteNumber'] != null) InfoRow('Credit note', '${done['creditNoteNumber']}'),
          ]),
          actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('Done'))],
        ),
      );

  void _step(_ReturnRow r, double to) => setState(() => r.quantity = roundQty(math.max(0, math.min(r.left, to))));

  Widget _row(_ReturnRow r) {
    final line = r.line;
    final discount = _n(line['discountPercent']);
    return Card(
      margin: const EdgeInsets.only(bottom: ErpSpace.s2),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(ErpSpace.s3, ErpSpace.s2, ErpSpace.s2, ErpSpace.s1),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('${line['description']}', style: const TextStyle(fontWeight: FontWeight.w600)),
          Text('${_m(line['unitPrice'])}${discount > 0 ? ' · $discount% off' : ''} · ${_q(r.left)} of ${_q(line['quantity'])} can be returned', style: Theme.of(context).textTheme.bodySmall),
          Row(children: [
            IconButton(tooltip: 'One less ${line['description']}', onPressed: r.quantity <= 0 ? null : () => _step(r, r.quantity - 1), icon: const Icon(Icons.remove_circle_outline)),
            SizedBox(width: 32, child: Text(_q(r.quantity), textAlign: TextAlign.center, style: const TextStyle(fontWeight: FontWeight.w700))),
            IconButton(tooltip: 'One more ${line['description']}', onPressed: r.quantity >= r.left ? null : () => _step(r, r.quantity + 1), icon: const Icon(Icons.add_circle_outline)),
            const Spacer(),
            Text(_m(returnAmount(line, r.quantity)), style: const TextStyle(fontWeight: FontWeight.w700)),
          ]),
          if (line['itemId'] != null)
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              dense: true,
              title: const Text('Back on shelf'),
              value: r.restock,
              onChanged: (v) => setState(() => r.restock = v),
            ),
        ]),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final o = _order;
    final refunds = _refunds;
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Return against a receipt')),
      body: SafeArea(
        child: ListView(padding: const EdgeInsets.all(ErpSpace.s4), keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag, children: [
          if (widget.session['registerName'] != null) Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: Text('Money comes out of the shift on ${widget.session['registerName']}.')),
          Row(children: [
            Expanded(
              child: TextField(
                key: const ValueKey('pos-return-number'),
                controller: _number,
                textInputAction: TextInputAction.search,
                decoration: const InputDecoration(labelText: 'Receipt number', hintText: 'Scan the receipt or type its number', border: OutlineInputBorder(), prefixIcon: Icon(Icons.qr_code_scanner)),
                onSubmitted: (_) => _find(),
              ),
            ),
            const SizedBox(width: ErpSpace.s2),
            FilledButton(key: const ValueKey('pos-return-find'), onPressed: _finding ? null : _find, child: const Text('Find')),
          ]),
          const SizedBox(height: ErpSpace.s3),
          if (_error != null)
            Container(
              key: const ValueKey('pos-return-error'),
              margin: const EdgeInsets.only(bottom: ErpSpace.s3),
              padding: const EdgeInsets.all(ErpSpace.s3),
              decoration: BoxDecoration(color: scheme.errorContainer, borderRadius: BorderRadius.circular(8)),
              child: Text(_error!, style: TextStyle(color: scheme.onErrorContainer)),
            ),
          if (o != null) ...[
            InfoCard(title: '${o['number']}', rows: [
              InfoRow('Date', formatDate(o['orderDate'])),
              InfoRow('Customer', text(o['customerName'])),
              InfoRow('Total', _m(o['totalAmount'])),
              if (_n(o['returnedTotal']) > 0) InfoRow('Already returned', _m(o['returnedTotal'])),
            ]),
            if (o['status'] != 'completed')
              const Text('Only a completed sale can be returned.')
            else if (_rows.every((r) => r.left <= 0))
              const Text('Everything on this sale has already been returned.')
            else ...[
              for (final r in _rows.where((r) => r.left > 0)) _row(r),
              const SizedBox(height: ErpSpace.s2),
              TextField(
                key: const ValueKey('pos-return-reason'),
                controller: _reason,
                maxLength: 500,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(labelText: 'Why did it come back? *', hintText: 'For example, wrong size', border: OutlineInputBorder(), counterText: ''),
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: ErpSpace.s3),
              Text('Give the money back as', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: ErpSpace.s1),
              Wrap(spacing: ErpSpace.s2, children: [
                for (final m in refundMethods(o)) ChoiceChip(key: ValueKey('pos-refund-$m'), label: Text(_tender(m)), selected: _method == m, onSelected: (_) => setState(() => _method = m)),
              ]),
              if (_method == 'card' || _method == 'other')
                Padding(
                  padding: const EdgeInsets.only(top: ErpSpace.s2),
                  child: TextField(controller: _reference, maxLength: 100, decoration: const InputDecoration(labelText: 'Reference (optional)', border: OutlineInputBorder(), counterText: ''), onChanged: (_) => setState(() {})),
                ),
              const SizedBox(height: ErpSpace.s3),
              InfoRow('To give back', _m(_total), bold: true),
              if (refunds.length > 1)
                Text('This sale was paid part on account, so it goes back the same way: ${refunds.map((f) => '${_tender(f['method'])} ${_m(f['amount'])}').join(' and ')}.', key: const ValueKey('pos-return-split')),
            ],
          ],
        ]),
      ),
      bottomNavigationBar: o == null
          ? null
          : SafeArea(
              maintainBottomViewPadding: true,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, ErpSpace.s3),
                child: FilledButton.icon(
                  key: const ValueKey('pos-return-confirm'),
                  style: FilledButton.styleFrom(backgroundColor: scheme.error, foregroundColor: scheme.onError),
                  onPressed: _canSubmit ? _submit : null,
                  icon: const Icon(Icons.assignment_return_outlined),
                  label: Text('Refund ${_m(_total)}'),
                ),
              ),
            ),
    );
  }
}

// ---------------------------------------------------------------- the sale

/// The body of `POST orders`: no unit price unless it was overridden (the server prices the lines), and payments that add up to the total.
Map<String, dynamic> saleBody(Json session, {required List<CartLine> lines, required List<Tender> tenders, String? customerId, String customerName = walkInCustomer, String? parkedOrderId}) => {
      'legalEntityId': session['legalEntityId'],
      'registerId': session['registerId'],
      'sessionId': session['id'],
      'customerPartyId': customerId,
      'customerName': customerId == null ? customerName : null,
      'lines': [for (final l in lines) toOrderLine(l)],
      'payments': toPayments(cartTotals(lines).total, tenders),
      'parkedOrderId': parkedOrderId,
    };

/// The body of `POST orders/park`: the ticket without any money.
Map<String, dynamic> holdBody(Json session, {required List<CartLine> lines, String? customerId, String customerName = walkInCustomer}) => {
      'sessionId': session['id'],
      'customerPartyId': customerId,
      'customerName': customerId == null ? customerName : null,
      'lines': [for (final l in lines) toOrderLine(l)],
    };

/// The ticket: scan or find items, name the customer, then hold it or take the money.
class PosSaleScreen extends ConsumerStatefulWidget {
  const PosSaleScreen({super.key, required this.session, this.settings, this.held});

  final Json session;

  /// `settings` of `GET terminal`: the discount limits and the negative stock rule.
  final Json? settings;

  /// A held sale to start from.
  final Json? held;

  @override
  ConsumerState<PosSaleScreen> createState() => _PosSaleState();
}

class _PosSaleState extends ConsumerState<PosSaleScreen> {
  final _scan = TextEditingController();
  final _scanFocus = FocusNode();

  /// Every item this ticket has seen, by barcode and SKU, so a repeat scan goes on the ticket without a round trip.
  final _known = <String, Json>{};

  List<CartLine> _lines = [];
  String? _customerId;
  String _customerName = walkInCustomer;
  String? _parkedOrderId;
  String _idempotencyKey = newIdempotencyKey();
  bool _holding = false;

  String get _registerId => '${widget.session['registerId']}';

  bool _can(String permission) => ref.read(meOrEmptyProvider).can(permission);

  double get _discountLimit => discountLimit(widget.settings, canOverride: _can('pos.orders.discount-override'));

  @override
  void initState() {
    super.initState();
    final held = widget.held;
    if (held != null) WidgetsBinding.instance.addPostFrameCallback((_) => _resume(held));
  }

  @override
  void dispose() {
    _scan.dispose();
    _scanFocus.dispose();
    super.dispose();
  }

  void _setCart(List<CartLine> lines) {
    setState(() {
      _lines = lines;
      _idempotencyKey = newIdempotencyKey(); // a different ticket is a different sale
      if (lines.isEmpty) _parkedOrderId = null; // the held copy stays on hold: it was not sold
    });
  }

  void _clearTicket() {
    _setCart([]);
    setState(() {
      _customerId = null;
      _customerName = walkInCustomer;
    });
  }

  // ---- items ----

  Future<List<Json>> _catalog(Map<String, dynamic> query) async {
    final page = await ref.read(apiProvider).get('$posPath/terminal/catalog', query: {'registerId': _registerId, ...query}) as Json;
    final items = (page['items'] as List<dynamic>).cast<Json>();
    items.forEach(_remember);
    return items;
  }

  void _remember(Json item) {
    _known['${item['sku']}'.toLowerCase()] = item;
    if (item['barcode'] != null) _known['${item['barcode']}'.toLowerCase()] = item;
  }

  /// The box empties at once, so a scanner can fire the next code before this one has come back.
  Future<void> _submitScan(String raw) async {
    final scan = parseScan(raw);
    if (scan.code.isEmpty) return;
    _scan.clear();
    _scanFocus.requestFocus();
    final hit = _known[scan.code.toLowerCase()];
    if (hit != null) {
      _addItem(hit, scan.quantity);
      return;
    }
    try {
      final exact = await _catalog({'code': scan.code, 'pageSize': 2});
      if (exact.isNotEmpty) {
        _addItem(exact.first, scan.quantity);
        return;
      }
      // Not a code: the one product whose name matches is what was meant.
      final found = await _catalog({'search': scan.code, 'pageSize': 2});
      if (found.length == 1) {
        _addItem(found.single, scan.quantity);
      } else if (found.isEmpty) {
        await _notFound(scan.code);
      } else {
        await _browse(search: scan.code, quantity: scan.quantity);
      }
    } on ApiException catch (e) {
      if (mounted) _say(context, e.message);
    }
  }

  /// Not a product: a receipt scanned here starts its return, for whoever may take one.
  Future<void> _notFound(String code) async {
    void warn() {
      if (!mounted) return;
      if (_scan.text.isEmpty) _scan.text = code;
      _say(context, 'No product matches "$code".');
    }

    if (!_can('pos.orders.refund') || !_can('pos.orders.read')) return warn();
    try {
      final order = await ref.read(apiProvider).get('$posPath/orders/by-number/${Uri.encodeComponent(code)}') as Json;
      if (mounted) _startReturn('${order['number']}');
    } on ApiException {
      warn();
    }
  }

  Future<void> _browse({String? search, double quantity = 1}) async {
    final picked = await showLookupSheet(
      context,
      ref,
      path: '$posPath/terminal/catalog',
      title: 'Find an item',
      subtitleKey: 'sku',
      query: {'registerId': _registerId, 'search': ?search},
    );
    if (picked == null) return;
    _remember(picked);
    _addItem(picked, quantity);
  }

  void _addItem(Json item, double quantity) {
    if (!mounted) return;
    if (item['price'] == null) {
      _say(context, '${item['name']} has no sales price.');
      return;
    }
    final onHand = toNum(item['onHand']);
    if (widget.settings?['allowNegativeStock'] == false && onHand != null && onHand <= 0) {
      _say(context, '${item['name']} is out of stock.');
      return;
    }
    final lines = [..._lines];
    final at = lines.indexWhere((l) => l.itemId == item['id']);
    if (at >= 0) {
      lines[at] = lines[at].copyWith(quantity: roundQty(lines[at].quantity + quantity), onHand: onHand?.toDouble());
    } else {
      lines.add(lineFromCatalog(item, quantity));
    }
    _setCart(lines);
  }

  void _changeQuantity(int index, double quantity) {
    final lines = [..._lines];
    if (quantity <= 0) {
      lines.removeAt(index);
    } else {
      lines[index] = lines[index].copyWith(quantity: roundQty(quantity));
    }
    _setCart(lines);
  }

  Future<void> _editLine(int index) async {
    final line = _lines[index];
    final limit = _discountLimit;
    final quantity = TextEditingController(text: _plain(line.quantity));
    final discount = TextEditingController(text: _plain(line.discountPercent));
    String? quantityError;
    String? discountError;
    final apply = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => AlertDialog(
          title: Text(line.description),
          content: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(
              key: const ValueKey('pos-edit-quantity'),
              controller: quantity,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(labelText: 'Quantity', border: const OutlineInputBorder(), errorText: quantityError),
            ),
            const SizedBox(height: ErpSpace.s3),
            TextField(
              key: const ValueKey('pos-edit-discount'),
              controller: discount,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(labelText: 'Discount %', helperText: 'Your limit is ${_q(limit)}%.', border: const OutlineInputBorder(), errorText: discountError),
            ),
          ]),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
            FilledButton(
              onPressed: () {
                final q = toNum(quantity.text);
                final d = toNum(discount.text.isEmpty ? '0' : discount.text);
                setLocal(() {
                  quantityError = q == null || q < 0 ? 'Enter a quantity' : null;
                  discountError = d == null || d < 0 ? 'Enter a percentage' : (d > limit ? 'Above your limit of ${_q(limit)}%' : null);
                });
                if (quantityError == null && discountError == null) Navigator.pop(ctx, true);
              },
              child: const Text('Apply'),
            ),
          ],
        ),
      ),
    );
    if (apply != true || !mounted) return;
    final q = toNum(quantity.text)!.toDouble();
    if (q <= 0) {
      _changeQuantity(index, 0);
      return;
    }
    final lines = [..._lines];
    lines[index] = lines[index].copyWith(quantity: roundQty(q), discountPercent: toNum(discount.text.isEmpty ? '0' : discount.text)!.toDouble());
    _setCart(lines);
  }

  // ---- customer ----

  Future<void> _pickCustomer() async {
    final choice = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          ListTile(key: const ValueKey('pos-customer-find'), leading: const Icon(Icons.search), title: const Text('Find a customer'), subtitle: const Text('By name, phone or code'), onTap: () => Navigator.pop(ctx, 'find')),
          ListTile(leading: const Icon(Icons.edit_outlined), title: const Text('Just a name on the receipt'), subtitle: const Text('No account'), onTap: () => Navigator.pop(ctx, 'name')),
          ListTile(leading: const Icon(Icons.person_outline), title: const Text(walkInCustomer), onTap: () => Navigator.pop(ctx, 'walkIn')),
        ]),
      ),
    );
    if (!mounted) return;
    if (choice == 'find') {
      final picked = await showLookupSheet(context, ref, path: '$posPath/terminal/customers', title: 'Find a customer', subtitleKey: 'code');
      if (picked != null) {
        setState(() {
          _customerId = '${picked['id']}';
          _customerName = '${picked['name']}';
        });
      }
    } else if (choice == 'name') {
      final name = await _askName();
      if (name != null) {
        setState(() {
          _customerId = null;
          _customerName = name;
        });
      }
    } else if (choice == 'walkIn') {
      setState(() {
        _customerId = null;
        _customerName = walkInCustomer;
      });
    }
  }

  Future<String?> _askName() {
    final controller = TextEditingController(text: _customerId == null && _customerName != walkInCustomer ? _customerName : '');
    return showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Name on the receipt'),
        content: TextField(controller: controller, autofocus: true, maxLength: 150, textCapitalization: TextCapitalization.words, decoration: const InputDecoration(border: OutlineInputBorder(), counterText: '')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, controller.text.trim().isEmpty ? null : controller.text.trim()), child: const Text('Use this name')),
        ],
      ),
    );
  }

  // ---- holding ----

  Future<void> _hold() async {
    if (_lines.isEmpty || _holding) return;
    setState(() => _holding = true);
    final api = ref.read(apiProvider);
    try {
      await api.post('$posPath/orders/park', body: holdBody(widget.session, lines: _lines, customerId: _customerId, customerName: _customerName));
      // A resumed ticket held again replaces its old copy.
      final previous = _parkedOrderId;
      if (previous != null) {
        try {
          await api.delete('$posPath/orders/$previous/park');
        } on ApiException {
          // The new copy is safe; at worst the old one stays listed and can be discarded by hand.
        }
      }
      ref.read(dataVersionProvider.notifier).bump();
      if (!mounted) return;
      _clearTicket();
      _say(context, 'Sale held.');
    } on ApiException catch (e) {
      if (mounted) _say(context, e.message);
    } finally {
      if (mounted) setState(() => _holding = false);
    }
  }

  Future<void> _openHeld() async {
    final order = await Navigator.of(context).push<Json>(MaterialPageRoute(builder: (_) => PosHeldSalesScreen(registerId: _registerId, onTicket: _parkedOrderId)));
    if (order == null || !mounted) return;
    if (_lines.isNotEmpty) {
      _say(context, 'Finish or hold the current ticket first.');
      return;
    }
    await _resume(order);
  }

  /// Today's prices, tax and stock, because the server charges today's prices at payment.
  Future<void> _resume(Json order) async {
    final canOverride = _can('pos.orders.override-price');
    final held = fromHeldOrder(order);
    final fresh = await Future.wait(held.map((l) async {
      if (l.itemId == null) return l;
      try {
        final items = await _catalog({'code': l.itemCode, 'pageSize': 2});
        return repriceHeld(l, items.where((i) => i['id'] == l.itemId).firstOrNull, canOverridePrice: canOverride);
      } on ApiException {
        return l;
      }
    }));
    if (!mounted) return;
    _setCart(fresh);
    setState(() {
      _parkedOrderId = '${order['id']}';
      _customerId = order['customerPartyId']?.toString();
      _customerName = '${order['customerName'] ?? walkInCustomer}';
    });
    if ([for (var i = 0; i < fresh.length; i++) fresh[i].unitPrice != held[i].unitPrice].any((changed) => changed)) {
      _say(context, 'Prices changed since this sale was held. The ticket shows the current prices.');
    }
  }

  // ---- returns and payment ----

  void _startReturn([String? number]) {
    Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PosReturnScreen(session: {'id': widget.session['id'], 'registerName': widget.session['registerName']}, initialNumber: number)));
  }

  Future<void> _pay() async {
    if (_lines.isEmpty) return;
    final sold = await Navigator.of(context).push<Json>(
      MaterialPageRoute(
        builder: (_) => _PaymentScreen(
          total: cartTotals(_lines).total,
          customerName: _customerName,
          hasCustomer: _customerId != null,
          canUseAccount: _can('pos.orders.on-account'),
          submit: (tenders) async => await ref.read(apiProvider).post(
                '$posPath/orders',
                headers: {'Idempotency-Key': _idempotencyKey},
                body: saleBody(widget.session, lines: _lines, tenders: tenders, customerId: _customerId, customerName: _customerName, parkedOrderId: _parkedOrderId),
              ) as Json,
        ),
      ),
    );
    if (sold == null || !mounted) return;
    _clearTicket();
    ref.read(dataVersionProvider.notifier).bump();
    await _showReceipt(sold);
  }

  Future<void> _showReceipt(Json order) {
    final payments = (order['payments'] as List<dynamic>? ?? const []).cast<Json>();
    final change = payments.fold<num>(0, (a, p) => a + _n(p['changeAmount']));
    return showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Sale ${order['number']}'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          for (final p in payments) InfoRow(_tender(p['method']), _m(p['amount'])),
          InfoRow('Total', _m(order['totalAmount']), bold: true),
          if (change > 0) InfoRow('Change to give', formatMoney(change), bold: true),
          if (order['invoiceNumber'] != null) InfoRow('Invoice', '${order['invoiceNumber']}'),
        ]),
        actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('New sale'))],
      ),
    );
  }

  Future<void> _confirmLeave() async {
    final leave = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text('Leave this ticket? Nothing was charged.${_parkedOrderId != null ? ' The held copy stays on hold.' : ' Hold it to keep it.'}'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Stay')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Leave')),
        ],
      ),
    );
    if (leave == true && mounted) Navigator.of(context).pop();
  }

  // ---- screen ----

  Widget _line(int index) {
    final l = _lines[index];
    final meta = [
      '${_m(l.unitPrice)} each',
      if (l.discountPercent > 0) '${_q(l.discountPercent)}% off',
      if (l.onHand != null) '${_q(l.onHand)} in stock',
    ].join(' · ');
    return Padding(
      key: ValueKey(l.key),
      padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4),
      child: Row(children: [
        Expanded(
          child: InkWell(
            onTap: () => _editLine(index),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: ErpSpace.s2),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(l.description, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w600)),
                Text(meta, style: Theme.of(context).textTheme.bodySmall),
              ]),
            ),
          ),
        ),
        IconButton(tooltip: 'One less ${l.description}', onPressed: () => _changeQuantity(index, l.quantity - 1), icon: const Icon(Icons.remove_circle_outline)),
        SizedBox(width: 30, child: Text(_q(l.quantity), textAlign: TextAlign.center, style: const TextStyle(fontWeight: FontWeight.w700))),
        IconButton(tooltip: 'One more ${l.description}', onPressed: () => _changeQuantity(index, l.quantity + 1), icon: const Icon(Icons.add_circle_outline)),
        SizedBox(width: 72, child: Text(_m(amountsOf(l).total), textAlign: TextAlign.end, style: const TextStyle(fontWeight: FontWeight.w700))),
      ]),
    );
  }

  @override
  Widget build(BuildContext context) {
    final totals = cartTotals(_lines);
    return PopScope(
      canPop: _lines.isEmpty,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _confirmLeave();
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text('${widget.session['registerName'] ?? 'New sale'}'),
          actions: [
            if (_can('pos.orders.refund') && _can('pos.orders.read')) IconButton(tooltip: 'Return', icon: const Icon(Icons.assignment_return_outlined), onPressed: _startReturn),
            IconButton(key: const ValueKey('pos-held-open'), tooltip: 'Held sales', icon: const Icon(Icons.pause_circle_outline), onPressed: _openHeld),
          ],
        ),
        body: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s4, ErpSpace.s1),
            child: TextField(
              key: const ValueKey('pos-scan'),
              controller: _scan,
              focusNode: _scanFocus,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                labelText: 'Scan or type a code',
                hintText: '3*ABC sells three of ABC',
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.qr_code_scanner),
                suffixIcon: IconButton(key: const ValueKey('pos-browse'), tooltip: 'Find an item', icon: const Icon(Icons.search), onPressed: _browse),
              ),
              onSubmitted: _submitScan,
            ),
          ),
          ListTile(
            key: const ValueKey('pos-customer'),
            leading: const Icon(Icons.person_outline),
            title: Text(_customerName, maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: _customerId != null ? const Text('Customer with an account') : null,
            trailing: const Icon(Icons.chevron_right),
            onTap: _pickCustomer,
          ),
          const Divider(height: 1),
          Expanded(
            child: _lines.isEmpty
                ? const ErpStateView.empty(icon: Icons.shopping_cart_outlined, title: 'Empty ticket', message: 'Scan a barcode, type a code or search for an item.', compact: true)
                : ListView.separated(itemCount: _lines.length, itemBuilder: (_, i) => _line(i), separatorBuilder: (_, _) => const Divider(height: 1)),
          ),
        ]),
        bottomNavigationBar: _lines.isEmpty
            ? null
            : SafeArea(
                maintainBottomViewPadding: true,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s1, ErpSpace.s4, ErpSpace.s3),
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                    if (totals.discount > 0) InfoRow('Discount', '-${formatMoney(totals.discount)}'),
                    if (totals.tax > 0) InfoRow('Tax', formatMoney(totals.tax)),
                    Row(children: [
                      Expanded(
                        flex: 2,
                        child: OutlinedButton.icon(key: const ValueKey('pos-hold'), onPressed: _holding ? null : _hold, icon: const Icon(Icons.pause), label: const Text('Hold')),
                      ),
                      const SizedBox(width: ErpSpace.s2),
                      Expanded(
                        flex: 3,
                        child: FilledButton.icon(
                          key: const ValueKey('pos-pay'),
                          onPressed: _can('pos.orders.create') ? _pay : null,
                          icon: const Icon(Icons.payments_outlined),
                          label: FittedBox(fit: BoxFit.scaleDown, child: Text('Pay ${formatMoney(totals.total)}', maxLines: 1)),
                        ),
                      ),
                    ]),
                  ]),
                ),
              ),
      ),
    );
  }
}

/// Taking the money. Enter what is handed over: more than is due works out the change; less adds that tender and asks for the rest, so a ticket can be split.
class _PaymentScreen extends StatefulWidget {
  const _PaymentScreen({required this.total, required this.customerName, required this.hasCustomer, required this.canUseAccount, required this.submit});

  final double total;
  final String customerName;
  final bool hasCustomer;
  final bool canUseAccount;
  final Future<Json> Function(List<Tender> tenders) submit;

  @override
  State<_PaymentScreen> createState() => _PaymentState();
}

class _PaymentState extends State<_PaymentScreen> {
  late final _amount = TextEditingController(text: widget.total.toStringAsFixed(2));
  final _reference = TextEditingController();

  String _method = 'cash';
  List<Tender> _tenders = [];
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  double get _entered => round2(toNum(_amount.text) ?? 0);
  double get _remaining => remainingDue(widget.total, _tenders);

  List<Tender> get _withEntry => [..._tenders, Tender(_method, _entered, _reference.text.trim().isEmpty ? null : _reference.text.trim())];

  /// Why what is typed cannot be taken.
  String? get _blocked {
    if (_method == 'account' && !widget.hasCustomer) return 'Choose a customer on the ticket first.';
    if (_method != 'cash' && _entered > _remaining) return 'Only cash can be more than what is due.';
    return null;
  }

  bool get _settled => tendersSettle(widget.total, _tenders);
  bool get _finishes => _settled || (_blocked == null && _entered > 0 && tendersSettle(widget.total, _withEntry));
  bool get _canAct => !_busy && (_settled || (_blocked == null && _entered > 0));

  void _setAmount(double value) => _amount.text = value.toStringAsFixed(2);

  void _choose(String method) => setState(() {
        _method = method;
        _reference.clear();
        _setAmount(_remaining);
      });

  void _takeCash(double amount) {
    setState(() {
      _method = 'cash';
      _setAmount(amount);
    });
    _act();
  }

  Future<void> _act() async {
    if (!_canAct) return;
    if (!_settled) {
      final next = _withEntry;
      if (!tendersSettle(widget.total, next)) {
        // A part payment: keep it and ask for the rest.
        setState(() {
          _tenders = next;
          _reference.clear();
          _setAmount(_remaining);
        });
        return;
      }
      _tenders = next;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final order = await widget.submit(_tenders);
      if (mounted) Navigator.of(context).pop(order);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final blocked = _settled ? null : _blocked;
    final change = changeDue(widget.total, _settled ? _tenders : _withEntry);
    final showEntry = !_settled;
    return Scaffold(
      appBar: AppBar(title: const Text('Take payment')),
      body: SafeArea(
        child: ListView(padding: const EdgeInsets.all(ErpSpace.s4), keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag, children: [
          if (_error != null)
            Container(
              key: const ValueKey('pos-pay-error'),
              margin: const EdgeInsets.only(bottom: ErpSpace.s3),
              padding: const EdgeInsets.all(ErpSpace.s3),
              decoration: BoxDecoration(color: scheme.errorContainer, borderRadius: BorderRadius.circular(8)),
              child: Text(_error!, style: TextStyle(color: scheme.onErrorContainer)),
            ),
          Center(child: Text(_remaining > 0 ? 'To pay' : 'Paid', style: Theme.of(context).textTheme.titleSmall)),
          Center(child: Text(formatMoney(_remaining > 0 ? _remaining : widget.total), key: const ValueKey('pos-due'), style: Theme.of(context).textTheme.displaySmall?.copyWith(fontWeight: FontWeight.w700))),
          if (change > 0) Center(child: Text('Change to give: ${formatMoney(change)}', key: const ValueKey('pos-change'), style: const TextStyle(fontWeight: FontWeight.w700))),
          const SizedBox(height: ErpSpace.s3),
          if (showEntry) ...[
            Wrap(spacing: ErpSpace.s2, children: [
              for (final m in ['cash', 'card', 'other', if (widget.canUseAccount) 'account'])
                ChoiceChip(key: ValueKey('pos-tender-$m'), label: Text(_tender(m)), selected: _method == m, onSelected: (_) => _choose(m)),
            ]),
            const SizedBox(height: ErpSpace.s3),
            TextField(
              key: const ValueKey('pos-amount'),
              controller: _amount,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(labelText: _method == 'cash' ? 'Cash handed over' : 'Amount', border: const OutlineInputBorder()),
              onChanged: (_) => setState(() {}),
              onSubmitted: (_) => _act(),
            ),
            const SizedBox(height: ErpSpace.s2),
            if (_method == 'cash')
              Wrap(spacing: ErpSpace.s2, children: [
                OutlinedButton(key: const ValueKey('pos-exact'), onPressed: _busy ? null : () => _takeCash(_remaining), child: const Text('Exact')),
                for (final q in quickCash(_remaining)) OutlinedButton(onPressed: _busy ? null : () => _takeCash(q), child: Text(_q(q))),
              ]),
            if (_method == 'card' || _method == 'other')
              TextField(
                key: const ValueKey('pos-reference'),
                controller: _reference,
                maxLength: 100,
                decoration: InputDecoration(labelText: _method == 'card' ? 'Card approval reference (optional)' : 'Reference (optional)', border: const OutlineInputBorder(), counterText: ''),
              ),
            if (_method == 'card') const Padding(padding: EdgeInsets.only(top: ErpSpace.s2), child: Text('Take the payment on the card machine first, then confirm here.')),
            if (_method == 'account' && widget.hasCustomer) Text('Goes on the account of ${widget.customerName} as an invoice.', key: const ValueKey('pos-account-note')),
            if (blocked != null) Padding(padding: const EdgeInsets.only(top: ErpSpace.s2), child: Text(blocked, key: const ValueKey('pos-tender-blocked'), style: TextStyle(color: scheme.error))),
          ],
          if (_tenders.isNotEmpty) ...[
            const SizedBox(height: ErpSpace.s3),
            for (var i = 0; i < _tenders.length; i++)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(_tender(_tenders[i].method)),
                subtitle: _tenders[i].reference == null ? null : Text(_tenders[i].reference!),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  Text(formatMoney(_tenders[i].amount), style: const TextStyle(fontWeight: FontWeight.w600)),
                  IconButton(
                    tooltip: 'Remove ${_tender(_tenders[i].method)}',
                    icon: const Icon(Icons.close),
                    onPressed: _busy
                        ? null
                        : () => setState(() {
                              _tenders = [..._tenders]..removeAt(i);
                              _setAmount(_remaining);
                            }),
                  ),
                ]),
              ),
          ],
        ]),
      ),
      bottomNavigationBar: SafeArea(
        maintainBottomViewPadding: true,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, ErpSpace.s3),
          child: FilledButton.icon(
            key: const ValueKey('pos-confirm-payment'),
            onPressed: _canAct ? _act : null,
            icon: _busy ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2)) : Icon(_finishes ? Icons.check : Icons.add),
            label: Text(_finishes ? 'Complete sale' : 'Add ${_tender(_method)} ${formatMoney(_entered)}'),
          ),
        ),
      ),
    );
  }
}

/// The tickets put aside on a register. Resume pops with the chosen order. Nothing has been charged for any of them.
class PosHeldSalesScreen extends ConsumerStatefulWidget {
  const PosHeldSalesScreen({super.key, required this.registerId, this.onTicket});

  final String registerId;

  /// The held copy of the ticket already open: discarding it from here would leave that ticket pointing at nothing.
  final String? onTicket;

  @override
  ConsumerState<PosHeldSalesScreen> createState() => _PosHeldSalesState();
}

class _PosHeldSalesState extends ConsumerState<PosHeldSalesScreen> {
  List<Json>? _rows;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final rows = await ref.read(apiProvider).get('$posPath/terminal/held', query: {'registerId': widget.registerId}) as List<dynamic>;
      if (mounted) setState(() => _rows = rows.cast<Json>().where((o) => o['id'] != widget.onTicket).toList());
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _discard(Json order) async {
    final sure = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text('Discard the held sale for ${order['customerName']}? It cannot be brought back.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Keep it')),
          FilledButton(style: FilledButton.styleFrom(backgroundColor: Theme.of(ctx).colorScheme.error), onPressed: () => Navigator.pop(ctx, true), child: const Text('Discard')),
        ],
      ),
    );
    if (sure != true || !mounted) return;
    try {
      await ref.read(apiProvider).delete('$posPath/orders/${order['id']}/park');
      ref.read(dataVersionProvider.notifier).bump();
      if (!mounted) return;
      _say(context, 'Held sale discarded.');
      await _load();
    } on ApiException catch (e) {
      if (mounted) _say(context, e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final rows = _rows;
    return Scaffold(
      appBar: AppBar(title: const Text('Held sales')),
      body: _error != null
          ? ErrorView(message: _error!, onRetry: _load)
          : rows == null
              ? const ErpSkeleton.list()
              : rows.isEmpty
                  ? const ErpStateView.empty(icon: Icons.pause_circle_outline, title: 'Nothing on hold', message: 'Hold a ticket to put it aside and serve the next customer.')
                  : ListView.builder(
                      padding: const EdgeInsets.all(ErpSpace.s3),
                      itemCount: rows.length,
                      itemBuilder: (context, i) {
                        final o = rows[i];
                        final lines = (o['lines'] as List<dynamic>? ?? const []).cast<Json>();
                        return Card(
                          key: ValueKey('pos-held-${o['id']}'),
                          margin: const EdgeInsets.only(bottom: ErpSpace.s2),
                          child: Padding(
                            padding: const EdgeInsets.fromLTRB(ErpSpace.s3, ErpSpace.s3, ErpSpace.s3, ErpSpace.s1),
                            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              Row(children: [
                                Expanded(child: Text('${o['customerName']}', style: const TextStyle(fontWeight: FontWeight.w600))),
                                Text(_m(o['totalAmount']), style: const TextStyle(fontWeight: FontWeight.w700)),
                              ]),
                              Text('${formatDate(o['orderDate'])} · ${o['cashierName']}', style: Theme.of(context).textTheme.bodySmall),
                              Text(lines.map((l) => '${_q(l['quantity'])} × ${l['description']}').join(', '), maxLines: 2, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
                              Row(mainAxisAlignment: MainAxisAlignment.end, children: [
                                TextButton(onPressed: () => _discard(o), child: const Text('Discard')),
                                const SizedBox(width: ErpSpace.s2),
                                FilledButton(onPressed: () => Navigator.of(context).pop(o), child: const Text('Resume')),
                              ]),
                            ]),
                          ),
                        );
                      },
                    ),
    );
  }
}

// ---------------------------------------------------------------- terminal

class PosTerminalScreen extends ConsumerStatefulWidget {
  const PosTerminalScreen({super.key});

  @override
  ConsumerState<PosTerminalScreen> createState() => _PosTerminalState();
}

class _PosTerminalState extends ConsumerState<PosTerminalScreen> {
  List<Json> _registers = [];
  Json? _settings;
  Json? _register;
  Json? _session;
  int _heldCount = 0;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _boot();
  }

  /// Everything here asks only for the right to sell, so a cashier does not also need the item, tax code and register screens of the back office.
  Future<void> _boot() async {
    try {
      final t = await ref.read(apiProvider).get('$posPath/terminal') as Json;
      final all = (t['registers'] as List<dynamic>).cast<Json>();
      if (!mounted) return;
      setState(() {
        _registers = all;
        _settings = t['settings'] as Json?;
        _register = all.isEmpty ? null : all.first;
        _error = null;
      });
      await _loadRegister();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _loadRegister() async {
    final reg = _register;
    if (reg == null) return;
    final api = ref.read(apiProvider);
    try {
      final s = await api.get('$posPath/terminal/session', query: {'registerId': reg['id']});
      final held = await api.get('$posPath/terminal/held', query: {'registerId': reg['id']});
      if (!mounted) return;
      setState(() {
        _session = s is Json ? s : null;
        _heldCount = held is List ? held.length : 0;
        _error = null;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _sell({Json? held}) async {
    await Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PosSaleScreen(session: _session!, settings: _settings, held: held)));
    await _loadRegister();
  }

  Future<void> _openHeld() async {
    final order = await Navigator.of(context).push<Json>(MaterialPageRoute(builder: (_) => PosHeldSalesScreen(registerId: '${_register!['id']}')));
    if (order != null && mounted) {
      await _sell(held: order);
    } else {
      await _loadRegister();
    }
  }

  Future<void> _return() async {
    final s = _session!;
    await Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PosReturnScreen(session: {'id': s['id'], 'registerName': s['registerName']})));
    await _loadRegister();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _loadRegister());
    final me = ref.watch(meOrEmptyProvider);
    final s = _session;
    return Scaffold(
      appBar: AppBar(title: const Text('POS terminal')),
      body: _loading
          ? const ErpSkeleton.list()
          : _error != null && _register == null
              ? ErrorView(message: _error!, onRetry: _boot)
              : _registers.isEmpty
                  ? const Center(child: Text('No active register. Create one under Registers.'))
                  : ListView(padding: const EdgeInsets.all(ErpSpace.s4), children: [
                      DropdownButtonFormField<String>(
                        initialValue: _register!['id'].toString(),
                        isExpanded: true, // a long register name is cut, not pushed off the phone
                        decoration: const InputDecoration(labelText: 'Register', border: OutlineInputBorder()),
                        items: [for (final r in _registers) DropdownMenuItem(value: r['id'].toString(), child: Text('${r['name']}', overflow: TextOverflow.ellipsis))],
                        onChanged: (id) {
                          setState(() {
                            _register = _registers.firstWhere((r) => r['id'].toString() == id);
                            _session = null;
                            _heldCount = 0;
                          });
                          _loadRegister();
                        },
                      ),
                      const SizedBox(height: ErpSpace.s4),
                      if (s == null) ...[
                        const Text('No open shift on this register.'),
                        const SizedBox(height: ErpSpace.s3),
                        if (me.can('pos.sessions.manage'))
                          FilledButton.icon(
                            onPressed: () async => openForm<Object?>(context, openShiftForm(_register!, enforceFloat: _settings?['enforceFloatAtOpen'] != false)).then((_) => _loadRegister()),
                            icon: const Icon(Icons.lock_open),
                            label: const Text('Open shift'),
                          ),
                      ] else ...[
                        InfoCard(title: 'Open shift', rows: [
                          InfoRow('Cashier', text(s['cashierName'])),
                          InfoRow('Opening cash', _m(s['openingCash'])),
                          InfoRow('Sales', _m(s['totalSales'])),
                          if (_n(s['totalReturns']) != 0) InfoRow('Returns', _m(s['totalReturns'])),
                          InfoRow('Expected cash', _m(s['expectedCash']), bold: true),
                        ]),
                        if (me.can('pos.orders.create')) ...[
                          FilledButton.icon(key: const ValueKey('pos-new-sale'), onPressed: _sell, icon: const Icon(Icons.shopping_cart_checkout), label: const Text('New sale')),
                          const SizedBox(height: ErpSpace.s2),
                          OutlinedButton.icon(key: const ValueKey('pos-terminal-held'), onPressed: _openHeld, icon: const Icon(Icons.pause_circle_outline), label: Text(_heldCount > 0 ? 'Held sales ($_heldCount)' : 'Held sales')),
                          const SizedBox(height: ErpSpace.s2),
                        ],
                        if (me.can('pos.orders.refund') && me.can('pos.orders.read')) ...[
                          OutlinedButton.icon(key: const ValueKey('pos-terminal-return'), onPressed: _return, icon: const Icon(Icons.assignment_return_outlined), label: const Text('Return against a receipt')),
                          const SizedBox(height: ErpSpace.s2),
                        ],
                        if (me.can('pos.sessions.read'))
                          OutlinedButton.icon(
                            key: const ValueKey('pos-terminal-report'),
                            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => SessionDetailScreen(row: s))).then((_) => _loadRegister()),
                            icon: const Icon(Icons.receipt_long),
                            label: const Text('Shift report, cash in/out, close'),
                          ),
                      ],
                    ]),
    );
  }
}

// ---------------------------------------------------------------- analytics

class PosAnalyticsScreen extends ConsumerWidget {
  const PosAnalyticsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get('$posPath/kpis'),
        builder: (context, snap) => Scaffold(
          appBar: AppBar(title: const Text('POS analytics')),
          body: snap.connectionState != ConnectionState.done
              ? const ErpSkeleton.list()
              : snap.hasError
                  ? ErrorView(message: snap.error is ApiException ? (snap.error! as ApiException).message : '${snap.error}', onRetry: () {})
                  : _kpis(snap.data as Json),
        ),
      );

  Widget _kpis(Json k) => DetailBody(children: [
        InfoCard(title: 'Today', rows: [
          InfoRow('Sales', _m(k['todaySales']), bold: true),
          InfoRow('Orders', '${k['totalOrdersToday']}'),
          InfoRow('Average ticket', _m(k['averageTicket'])),
          InfoRow('Active registers', '${k['activeRegistersCount']}'),
          InfoRow('Open shifts', '${k['openSessionsCount']}'),
        ]),
      ]);
}
