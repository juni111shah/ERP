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
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

const posPath = '/api/v1/m/pos';

num _n(Object? v) => toNum(v) ?? 0;
String _m(Object? v) => formatMoney(_n(v));

const orderStatuses = [Option('completed', 'Completed'), Option('voided', 'Voided'), Option('parked', 'Parked')];
const sessionStatuses = [Option('open', 'Open'), Option('closed', 'Closed')];
const paymentMethods = [Option('cash', 'Cash'), Option('card', 'Card'), Option('giftCard', 'Gift card'), Option('account', 'On account'), Option('other', 'Other')];

// ---------------------------------------------------------------- registers

FormSpec registerForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New register',
    initial: {
      if (editing) ...{
        'name': existing['name'],
        'isActive': existing['isActive'],
        'receiptHeader': existing['receiptHeader'],
        'receiptFooter': existing['receiptFooter'],
        'warehouseId': existing['warehouseId'],
      },
      'legalEntityId': existing?['legalEntityId'] ?? me.defaultEntity?.id,
    },
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      if (!editing) Field.text('code', 'Code', required: true, maxLength: 50),
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.lookup('warehouseId', 'Warehouse (optional)', path: '/api/v1/m/inventory/warehouses', subtitleKey: 'code'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('receiptHeader', 'Receipt header', maxLength: 500),
      Field.multiline('receiptFooter', 'Receipt footer', maxLength: 500),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      if (editing) {
        // The update replaces the whole record: keep what the form does not edit.
        return api.put('$posPath/registers/${existing['id']}', body: {'defaultCashAccountId': existing['defaultCashAccountId'], 'warehouseId': null, ...body});
      }
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
        return DetailPage(
          path: '$posPath/registers/$id',
          titleOf: (d) => '${d['name']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('pos.registers.manage'),
          edit: (d) => registerForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['name']}', subtitle: '${d['code']}', status: d['isActive'] == true ? 'active' : 'inactive'),
            InfoCard(rows: [
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

FormSpec openShiftForm(Json register) => FormSpec(
      title: 'Open shift',
      intro: 'Register ${register['name']}. The cashier is you.',
      submitLabel: 'Open shift',
      fields: [
        Field.money('openingCash', 'Opening cash in drawer', required: true, initial: '0'),
        Field.multiline('notes', 'Notes', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$posPath/sessions/open', body: {...body, 'registerId': register['id'], 'legalEntityId': register['legalEntityId']}),
    );

FormSpec closeShiftForm(Json session) => FormSpec(
      title: 'Close shift',
      intro: 'Expected cash in the drawer: ${_m(session['expectedCash'])}. Count the drawer and enter what is there.',
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
      const SizedBox(height: 4),
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

Widget sessionBody(BuildContext context, Json d) {
  final tx = (d['drawerTransactions'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: '${d['registerName']}', subtitle: 'Cashier ${d['cashierName']} · opened ${formatDate(d['openedAt'])}', status: d['status']?.toString()),
    InfoCard(rows: [
      InfoRow('Opening cash', _m(d['openingCash'])),
      InfoRow('Sales', _m(d['totalSales'])),
      InfoRow('Returns / voids', _m(d['totalReturns'])),
      if (d.containsKey('totalPayIn')) InfoRow('Cash in', _m(d['totalPayIn'])),
      if (d.containsKey('totalPayOut')) InfoRow('Cash out', _m(d['totalPayOut'])),
      InfoRow('Expected cash', _m(d['expectedCash']), bold: true),
      if (d['status'] == 'closed') ...[
        InfoRow('Counted cash', _m(d['actualCash'])),
        InfoRow('Difference', _m(d['cashDifference']), bold: true),
        InfoRow('Closed', formatDate(d['closedAt'])),
      ],
      InfoRow('Notes', text(d['notes'])),
    ]),
    if (tx.isNotEmpty) ...[
      Text('Drawer movements', style: Theme.of(context).textTheme.titleMedium),
      for (final t in tx)
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(humanize('${t['type']}')),
          subtitle: Text('${t['reason']}'),
          trailing: Text(_m(t['amount'])),
        ),
    ],
  ]);
}

/// A shift. The API has no read-one endpoint: an open shift is read through "current" (it carries the drawer movements), a closed one shows its list row.
class SessionDetailScreen extends StatelessWidget {
  const SessionDetailScreen({super.key, required this.row});

  final Json row;

  @override
  Widget build(BuildContext context) {
    if (row['status'] != 'open') {
      return Scaffold(appBar: AppBar(title: Text('${row['registerName']}')), body: sessionBody(context, row));
    }
    return DetailPage(
      path: '$posPath/sessions/current?registerId=${row['registerId']}',
      titleOf: (d) => '${d['registerName']}',
      body: sessionBody,
      actions: [
        ActionSpec(label: 'Cash in/out', icon: Icons.swap_vert, permission: 'pos.sessions.manage', visible: (d) => d['status'] == 'open', form: cashMovementForm, run: (ref, d, _) async => null),
        ActionSpec(label: 'Close shift', icon: Icons.lock_outline, permission: 'pos.sessions.manage', destructive: true, visible: (d) => d['status'] == 'open', form: closeShiftForm, run: (ref, d, _) async => null),
      ],
    );
  }
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
      const SizedBox(height: 4),
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

class OrderDetailScreen extends StatelessWidget {
  const OrderDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$posPath/orders/$id',
        titleOf: (d) => '${d['number']}',
        body: orderBody,
        actions: [
          ActionSpec(
            label: 'Void',
            icon: Icons.block,
            permission: 'pos.orders.void',
            destructive: true,
            visible: (d) => d['status'] == 'completed',
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
      InfoRow('Payment', humanize('${d['paymentStatus']}')),
      InfoRow('Notes', text(d['notes'])),
    ]),
    Text('Items', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${formatMoney(_n(l['quantity']))} × ${_m(l['unitPrice'])}${_n(l['discountPercent']) > 0 ? ' · ${_n(l['discountPercent'])}% off' : ''}'),
        trailing: Text(_m(l['lineTotal'])),
      ),
    if (payments.isNotEmpty) ...[
      const SizedBox(height: 8),
      Text('Payments', style: Theme.of(context).textTheme.titleMedium),
      for (final p in payments)
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(humanize('${p['method']}')),
          subtitle: _n(p['changeAmount']) > 0 ? Text('Tendered ${_m(p['tenderedAmount'])} · change ${_m(p['changeAmount'])}') : null,
          trailing: Text(_m(p['amount'])),
        ),
    ],
  ]);
}

// ---------------------------------------------------------------- the sale

num _round2(num v) => (v * 100).roundToDouble() / 100;

/// What the server will charge for these lines (price and tax from the catalog, as the server does it). Used only to fill the payment amount.
Future<num> computeOrderTotal(WidgetRef ref, List<Json> lines) async {
  final api = ref.read(apiProvider);
  final rates = <String, num>{};
  final codes = await api.get('/api/v1/finance/tax-codes', query: {'pageSize': 200});
  for (final c in ((codes as Json)['items'] as List<dynamic>? ?? const [])) {
    rates[(c as Json)['id'].toString()] = _n(c['rate']);
  }
  num total = 0;
  for (final l in lines) {
    final item = await api.get('/api/v1/items/${l['itemId']}') as Json;
    final raw = _round2(_n(l['quantity']) * _n(item['salesPrice']));
    final after = raw - _round2(raw * _n(l['discountPercent']) / 100);
    final rate = item['taxCodeId'] == null ? 0 : (rates[item['taxCodeId'].toString()] ?? 0);
    total += after + _round2(after * rate / 100);
  }
  return _round2(total);
}

/// The body for POST orders: no unit price (the server prices the lines), one payment for the whole total.
Map<String, dynamic> saleBody(Json session, Json form, num total) {
  final method = form['paymentMethod'] ?? 'cash';
  final tendered = method == 'cash' ? (form['tendered'] ?? 0) : 0;
  final lines = [
    for (final l in (form['lines'] as List<dynamic>).cast<Json>())
      {'itemId': l['itemId'], 'quantity': l['quantity'], if (_n(l['discountPercent']) > 0) 'discountPercent': l['discountPercent']},
  ];
  return {
    'legalEntityId': session['legalEntityId'],
    'registerId': session['registerId'],
    'sessionId': session['id'],
    if (form['customerName'] != null) 'customerName': form['customerName'],
    if (form['notes'] != null) 'notes': form['notes'],
    'lines': lines,
    'payments': [
      {'method': method, 'amount': total, 'tenderedAmount': tendered, 'changeAmount': math.max(0, _n(tendered) - total), if (form['reference'] != null) 'reference': form['reference']},
    ],
  };
}

FormSpec saleForm(Json session) => FormSpec(
      title: 'New sale',
      intro: 'Prices and tax come from the catalog. Cashier: you.',
      submitLabel: 'Charge',
      fields: [
        Field.lines('lines', 'Items', addLabel: 'Add item', fields: [
          Field.lookup('itemId', 'Item', path: '/api/v1/items', query: const {'filter': 'type:eq:product'}, subtitleKey: 'sku', required: true, copy: const {'price': 'salesPrice'}),
          Field.text('price', 'Catalog price', readOnly: true),
          Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
          Field.decimal('discountPercent', 'Discount %', min: 0, max: 100, decimals: 2),
        ]),
        Field.text('customerName', 'Customer name (optional)', maxLength: 150),
        Field.select('paymentMethod', 'Payment', options: paymentMethods, required: true, initial: 'cash'),
        Field.money('tendered', 'Cash tendered (leave empty for exact)', visibleWhen: (v) => v['paymentMethod'] == 'cash'),
        Field.text('reference', 'Payment reference', maxLength: 100, visibleWhen: (v) => v['paymentMethod'] != 'cash'),
        Field.multiline('notes', 'Notes', maxLength: 500),
      ],
      summary: (context, raw) {
        final est = [for (final l in (raw['lines'] as List<dynamic>)) _n((l as Json)['quantity']) * _n(l['price']) * (1 - _n(l['discountPercent']) / 100)].fold<num>(0, (a, b) => a + b);
        return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Subtotal before tax', formatMoney(est), bold: true));
      },
      submit: (ref, body, raw) async {
        final api = ref.read(apiProvider);
        final lines = (body['lines'] as List<dynamic>).cast<Json>();
        var total = await computeOrderTotal(ref, lines);
        if (body['paymentMethod'] == 'cash' && body['tendered'] != null && _n(body['tendered']) < total) {
          throw ApiException(422, 'Cash tendered (${formatMoney(_n(body['tendered']))}) is less than the total (${formatMoney(total)}).');
        }
        try {
          return await api.post('$posPath/orders', body: saleBody(session, body, total));
        } on ApiException catch (e) {
          // A one-cent rounding difference with the server: use the total the server names and try once more.
          final m = RegExp(r'order total \(([\d,]+\.\d+)\)').firstMatch(e.message);
          if (m == null) rethrow;
          total = num.parse(m.group(1)!.replaceAll(',', ''));
          return api.post('$posPath/orders', body: saleBody(session, body, total));
        }
      },
    );

// ---------------------------------------------------------------- terminal

class PosTerminalScreen extends ConsumerStatefulWidget {
  const PosTerminalScreen({super.key});

  @override
  ConsumerState<PosTerminalScreen> createState() => _PosTerminalState();
}

class _PosTerminalState extends ConsumerState<PosTerminalScreen> {
  List<Json> _registers = [];
  Json? _register;
  Json? _session;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _loadRegisters();
  }

  Future<void> _loadRegisters() async {
    try {
      final r = await ref.read(apiProvider).get('$posPath/registers', query: {'pageSize': 100}) as Json;
      final all = (r['items'] as List<dynamic>).cast<Json>().where((x) => x['isActive'] == true).toList();
      if (!mounted) return;
      setState(() {
        _registers = all;
        _register = all.isEmpty ? null : all.first;
      });
      await _loadSession();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _loadSession() async {
    final reg = _register;
    if (reg == null) return;
    try {
      final s = await ref.read(apiProvider).get('$posPath/sessions/current', query: {'registerId': reg['id']});
      if (mounted) setState(() => _session = s is Json ? s : null);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _sell() async {
    final order = await openForm<Object?>(context, saleForm(_session!));
    if (order is! Json || !mounted) return;
    final change = ((order['payments'] as List<dynamic>?) ?? const []).fold<num>(0, (a, p) => a + _n((p as Json)['changeAmount']));
    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Sale ${order['number']}'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          InfoRow('Total', _m(order['totalAmount']), bold: true),
          if (change > 0) InfoRow('Change to give', formatMoney(change), bold: true),
        ]),
        actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('Done'))],
      ),
    );
    await _loadSession();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _loadSession());
    final me = ref.watch(meOrEmptyProvider);
    final s = _session;
    return Scaffold(
      appBar: AppBar(title: const Text('POS terminal')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null && _register == null
              ? ErrorView(message: _error!, onRetry: _loadRegisters)
              : _registers.isEmpty
                  ? const Center(child: Text('No active register. Create one under Registers.'))
                  : ListView(padding: const EdgeInsets.all(16), children: [
                      DropdownButtonFormField<String>(
                        initialValue: _register!['id'].toString(),
                        decoration: const InputDecoration(labelText: 'Register', border: OutlineInputBorder()),
                        items: [for (final r in _registers) DropdownMenuItem(value: r['id'].toString(), child: Text('${r['name']}'))],
                        onChanged: (id) {
                          setState(() {
                            _register = _registers.firstWhere((r) => r['id'].toString() == id);
                            _session = null;
                          });
                          _loadSession();
                        },
                      ),
                      const SizedBox(height: 16),
                      if (s == null) ...[
                        const Text('No open shift on this register.'),
                        const SizedBox(height: 12),
                        if (me.can('pos.sessions.manage'))
                          FilledButton.icon(onPressed: () async => openForm<Object?>(context, openShiftForm(_register!)).then((_) => _loadSession()), icon: const Icon(Icons.lock_open), label: const Text('Open shift')),
                      ] else ...[
                        InfoCard(title: 'Open shift', rows: [
                          InfoRow('Cashier', text(s['cashierName'])),
                          InfoRow('Opening cash', _m(s['openingCash'])),
                          InfoRow('Sales', _m(s['totalSales'])),
                          InfoRow('Expected cash', _m(s['expectedCash']), bold: true),
                        ]),
                        if (me.can('pos.orders.create')) FilledButton.icon(onPressed: _sell, icon: const Icon(Icons.shopping_cart_checkout), label: const Text('New sale')),
                        const SizedBox(height: 8),
                        OutlinedButton.icon(
                          onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => SessionDetailScreen(row: s))).then((_) => _loadSession()),
                          icon: const Icon(Icons.receipt_long),
                          label: const Text('Shift summary, cash in/out, close'),
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
              ? const Center(child: CircularProgressIndicator())
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
