import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'receipt_screens.dart';
import 'report_screens.dart';

const partiesPath = '/api/v1/parties';
const partyGroupsPath = '/api/v1/party-groups';
const itemsPath = '/api/v1/items';

const partyRoles = [Option('Customer', 'Customer'), Option('Vendor', 'Vendor'), Option('Employee', 'Employee'), Option('Other', 'Other')];
const _roleKeys = {'Customer': 'roleCustomer', 'Vendor': 'roleVendor', 'Employee': 'roleEmployee', 'Other': 'roleOther'};

/// Create (existing == null) or edit a party. Roles are toggles; the API takes them as one comma separated string.
FormSpec partyForm(Json? existing, {String? defaultRole}) {
  final editing = existing != null;
  final roles = {for (final r in (existing?['roles'] as List<dynamic>? ?? const [])) r.toString()};
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New ${defaultRole == null ? 'party' : defaultRole.toLowerCase()}',
    initial: {
      if (editing) ...{
        'name': existing['name'],
        'kind': existing['kind'],
        'email': existing['email'],
        'phone': existing['phone'],
        'taxNumber': existing['taxNumber'],
        'idNumber': existing['idNumber'],
        'iban': existing['iban'],
        'currencyCode': existing['currencyCode'],
        'paymentTermsDays': existing['paymentTermsDays'],
        'address': existing['address'],
        'isActive': existing['isActive'],
        'partyGroupId': existing['partyGroupId'],
        if (existing['partyGroupId'] != null) '__label_partyGroupId': 'Selected group',
      },
      for (final e in _roleKeys.entries) e.value: editing ? roles.contains(e.key) : e.key == defaultRole,
    },
    fields: [
      Field.text('name', 'Name', required: true, maxLength: 200),
      Field.select('kind', 'Type', options: const [Option('Company', 'Company'), Option('Individual', 'Individual')], required: true, initial: 'Company'),
      for (final r in partyRoles) Field.toggle(_roleKeys[r.value]!, r.label),
      Field.text('email', 'Email', keyboard: TextInputType.emailAddress, validator: (v, _) {
        final s = v?.toString().trim() ?? '';
        return s.isEmpty || RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(s) ? null : 'Enter a valid email';
      }),
      Field.text('phone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
      Field.text('taxNumber', 'Tax number', maxLength: 50),
      Field.text('idNumber', 'ID number', maxLength: 50),
      Field.text('iban', 'IBAN', maxLength: 50),
      Field.text('currencyCode', 'Currency (3 letters)', maxLength: 3, validator: (v, _) {
        final s = v?.toString().trim() ?? '';
        return s.isEmpty || s.length == 3 ? null : 'Use a 3 letter currency code';
      }),
      Field.integer('paymentTermsDays', 'Payment terms (days)', min: 0, max: 3650),
      Field.lookup('partyGroupId', 'Party group', path: partyGroupsPath, subtitleKey: 'code'),
      Field.multiline('address', 'Address', maxLength: 1000),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) {
      final chosen = [for (final e in _roleKeys.entries) if (body[e.value] == true) e.key];
      if (chosen.isEmpty) throw ApiException(400, 'Choose at least one role (customer, vendor...).');
      final out = {...body}..removeWhere((k, _) => _roleKeys.containsValue(k));
      out['roles'] = chosen.join(', ');
      final api = ref.read(apiProvider);
      return editing ? api.put('$partiesPath/${existing['id']}', body: out) : api.post(partiesPath, body: out);
    },
  );
}

Widget partyTile(BuildContext context, Json p) {
  final roles = (p['roles'] as List<dynamic>? ?? const []).map((r) => r.toString()).join(' · ');
  final contact = [p['email'], p['phone']].where((x) => x != null && x.toString().isNotEmpty).join(' · ');
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => PartyDetailScreen(id: p['id'].toString()))),
    title: Text('${p['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${p['code']}${roles.isEmpty ? '' : ' · $roles'}${contact.isEmpty ? '' : '\n$contact'}'),
    isThreeLine: contact.isNotEmpty,
    trailing: p['isActive'] == false ? const StatusChip('Inactive', color: Colors.grey) : null,
  );
}

/// Parties filtered by role. [role] null = all parties (with role chips); a fixed role hides the chips.
class PartiesScreen extends ConsumerStatefulWidget {
  const PartiesScreen({super.key, this.title = 'Parties', this.role, this.showAppBar = true});

  final String title;
  final String? role;
  final bool showAppBar;

  @override
  ConsumerState<PartiesScreen> createState() => _PartiesScreenState();
}

class _PartiesScreenState extends ConsumerState<PartiesScreen> {
  String? _role;

  @override
  void initState() {
    super.initState();
    _role = widget.role;
  }

  @override
  Widget build(BuildContext context) {
    final canCreate = ref.watch(meOrEmptyProvider).can('core.parties.write');
    return Scaffold(
      appBar: widget.showAppBar
          ? AppBar(title: Text(widget.title), actions: [
              if (widget.role != 'Vendor' && ref.watch(meOrEmptyProvider).can('finance.reports.read'))
                IconButton(tooltip: 'Receivables ageing', icon: const Icon(Icons.hourglass_bottom), onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ArAgeingScreen()))),
            ])
          : null,
      floatingActionButton: canCreate ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, partyForm(null, defaultRole: _role)), icon: const Icon(Icons.add), label: const Text('New')) : null,
      body: Column(children: [
        if (widget.role == null)
          SizedBox(
            height: 48,
            child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6), children: [
              Padding(padding: const EdgeInsets.only(right: 8), child: ChoiceChip(label: const Text('All'), selected: _role == null, onSelected: (_) => setState(() => _role = null))),
              for (final o in partyRoles) Padding(padding: const EdgeInsets.only(right: 8), child: ChoiceChip(label: Text(o.label), selected: _role == o.value, onSelected: (_) => setState(() => _role = o.value))),
            ]),
          ),
        Expanded(child: PagedListView(path: partiesPath, itemBuilder: partyTile, searchHint: 'Search ${widget.title.toLowerCase()}', emptyText: 'Nothing found.', query: {if (_role != null) 'role': _role})),
      ]),
    );
  }
}

class PartyDetailScreen extends StatelessWidget {
  const PartyDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$partiesPath/$id',
          titleOf: (d) => '${d['name']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('core.parties.write'),
          edit: (d) => partyForm(d),
          body: (context, d) => _body(context, ref, d),
          actions: [
            ActionSpec(
              label: 'Record receipt',
              icon: Icons.payments_outlined,
              permission: 'finance.receipts.write',
              visible: (d) => (d['roles'] as List<dynamic>? ?? const []).contains('Customer') && d['isActive'] != false,
              form: (d) => receiptForm(ref, partyId: d['id'].toString(), partyName: d['name']?.toString()),
              run: (ref, d, _) async => null,
            ),
          ],
        );
      });

  Widget _body(BuildContext context, WidgetRef ref, Json d) {
    final isCustomer = (d['roles'] as List<dynamic>? ?? const []).contains('Customer');
    return DetailBody(children: [
      DetailHeader(title: d['name']?.toString() ?? '', subtitle: '${d['code']} · ${(d['roles'] as List<dynamic>? ?? const []).join(' · ')}', status: d['isActive'] == false ? 'inactive' : null),
      InfoCard(title: 'Contact', rows: [
        InfoRow('Type', text(d['kind'])),
        InfoRow('Email', text(d['email'])),
        InfoRow('Phone', text(d['phone'])),
        InfoRow('Address', text(d['address'])),
      ]),
      InfoCard(title: 'Tax and payment', rows: [
        InfoRow('Tax number', text(d['taxNumber'])),
        InfoRow('ID number', text(d['idNumber'])),
        InfoRow('IBAN', text(d['iban'])),
        InfoRow('Currency', text(d['currencyCode'])),
        InfoRow('Payment terms', d['paymentTermsDays'] == null ? null : '${d['paymentTermsDays']} days'),
      ]),
      if (isCustomer) PartyBalance(partyId: d['id'].toString(), partyName: d['name']?.toString() ?? ''),
    ]);
  }
}

/// Customer balance from the statement report, with a link to the full statement.
class PartyBalance extends ConsumerStatefulWidget {
  const PartyBalance({super.key, required this.partyId, required this.partyName});

  final String partyId;
  final String partyName;

  @override
  ConsumerState<PartyBalance> createState() => _PartyBalanceState();
}

class _PartyBalanceState extends ConsumerState<PartyBalance> {
  Json? _data;

  @override
  void initState() {
    super.initState();
    ref.read(apiProvider).get('/api/v1/finance/reports/customer-statement', query: {'partyId': widget.partyId, 'asOf': todayIsoOf(DateTime.now())}).then((d) {
      if (mounted) setState(() => _data = d as Json);
    }).catchError((_) {});
  }

  @override
  Widget build(BuildContext context) {
    final d = _data;
    if (d == null) return const SizedBox.shrink();
    return InfoCard(title: 'Receivable balance', rows: [
      InfoRow('Open balance', formatMoney((d['balance'] as num?) ?? 0), bold: true),
      InfoRow('Unallocated receipts', formatMoney((d['unallocatedReceipts'] as num?) ?? 0)),
      InfoRow('Open items', '${(d['items'] as List<dynamic>? ?? const []).length}'),
      InfoRow('', null),
    ]).withStatementButton(context, widget.partyId, widget.partyName);
  }
}

extension on Widget {
  Widget withStatementButton(BuildContext context, String id, String name) => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        this,
        Align(
          alignment: Alignment.centerRight,
          child: TextButton.icon(
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CustomerStatementScreen(partyId: id, partyName: name))),
            icon: const Icon(Icons.description_outlined),
            label: const Text('Customer statement'),
          ),
        ),
      ]);
}

// ---- party groups --------------------------------------------------------------------------------------------------

FormSpec partyGroupForm(Json? existing) => FormSpec(
      title: existing == null ? 'New party group' : 'Edit ${existing['name']}',
      initial: {if (existing != null) ...{'code': existing['code'], 'name': existing['name'], 'isActive': existing['isActive']}},
      fields: [
        Field.text('code', 'Code', required: true, maxLength: 50),
        Field.text('name', 'Name', required: true, maxLength: 200),
        Field.toggle('isActive', 'Active', initial: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post(partyGroupsPath, body: body) : api.put('$partyGroupsPath/${existing['id']}', body: body);
      },
    );

class PartyGroupsScreen extends ConsumerWidget {
  const PartyGroupsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(meOrEmptyProvider);
    final canWrite = me.can('core.partygroups.write');
    return Scaffold(
      appBar: AppBar(title: const Text('Party groups')),
      floatingActionButton: canWrite ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, partyGroupForm(null)), icon: const Icon(Icons.add), label: const Text('New')) : null,
      body: PagedListView(
        path: partyGroupsPath,
        searchHint: 'Search groups',
        emptyText: 'No party groups yet.',
        itemBuilder: (context, g) => ListTile(
          onTap: canWrite ? () => openForm<Object?>(context, partyGroupForm(g)) : null,
          title: Text('${g['name']}'),
          subtitle: Text('${g['code']}'),
          trailing: g['isActive'] == false ? const StatusChip('Inactive', color: Colors.grey) : null,
        ),
      ),
    );
  }
}

// ---- items ---------------------------------------------------------------------------------------------------------

FormSpec itemForm(Json? existing) => FormSpec(
      title: existing == null ? 'New item' : 'Edit ${existing['name']}',
      initial: {
        if (existing != null) ...{
          'sku': existing['sku'],
          'name': existing['name'],
          'type': existing['type'],
          'uom': existing['uom'],
          'salesPrice': existing['salesPrice'],
          'purchasePrice': existing['purchasePrice'],
          'taxCodeId': existing['taxCodeId'],
          if (existing['taxCodeId'] != null) '__label_taxCodeId': 'Selected tax code',
          'isActive': existing['isActive'],
        },
      },
      fields: [
        Field.text('sku', 'SKU', required: true, maxLength: 50),
        Field.text('name', 'Name', required: true, maxLength: 200),
        Field.select('type', 'Type', options: const [Option('product', 'Product'), Option('service', 'Service')], required: true, initial: 'product'),
        Field.text('uom', 'Unit of measure', required: true, maxLength: 20, initial: 'ea'),
        Field.money('salesPrice', 'Sales price'),
        Field.money('purchasePrice', 'Purchase price'),
        Field.lookup('taxCodeId', 'Tax code', path: '/api/v1/finance/tax-codes', subtitleKey: 'code'),
        Field.toggle('isActive', 'Active', initial: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post(itemsPath, body: body) : api.put('$itemsPath/${existing['id']}', body: body);
      },
    );

Widget itemTile(BuildContext context, Json i) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ItemDetailScreen(id: i['id'].toString()))),
      title: Text('${i['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${i['sku']} · ${humanize(i['type']?.toString())}'),
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        if (i['salesPrice'] != null) Text(formatMoney((i['salesPrice'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        if (i['isActive'] == false) const StatusChip('Inactive', color: Colors.grey),
      ]),
    );

class ItemsScreen extends  ConsumerWidget {
  const ItemsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canCreate = ref.watch(meOrEmptyProvider).can('core.items.write');
    return Scaffold(
      appBar: AppBar(title: const Text('Items')),
      floatingActionButton: canCreate ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, itemForm(null)), icon: const Icon(Icons.add), label: const Text('New')) : null,
      body: PagedListView(path: itemsPath, itemBuilder: itemTile, searchHint: 'Search items', emptyText: 'No items found.'),
    );
  }
}

class ItemDetailScreen extends StatelessWidget {
  const ItemDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$itemsPath/$id',
          titleOf: (d) => '${d['name']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('core.items.write'),
          edit: itemForm,
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: d['name']?.toString() ?? '', subtitle: '${d['sku']} · ${humanize(d['type']?.toString())}', status: d['isActive'] == false ? 'inactive' : null),
            InfoCard(rows: [
              InfoRow('Unit', text(d['uom'])),
              InfoRow('Sales price', d['salesPrice'] == null ? null : formatMoney((d['salesPrice'] as num))),
              InfoRow('Purchase price', d['purchasePrice'] == null ? null : formatMoney((d['purchasePrice'] as num))),
            ]),
          ]),
        );
      });
}
