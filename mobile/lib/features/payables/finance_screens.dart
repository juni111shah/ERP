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
import 'api_view.dart';
import 'common.dart';

// ---------------------------------------------------------------- cheque register

const chequesPath = '$finPath/cheques';
const chequeStatuses = [
  Option('inCustody', 'In custody'),
  Option('deposited', 'Deposited'),
  Option('cleared', 'Cleared'),
  Option('bounced', 'Bounced'),
  Option('replaced', 'Replaced'),
  Option('cancelled', 'Cancelled'),
  Option('issued', 'Issued'),
];

Widget chequeTile(BuildContext context, Json c) {
  final status = (c['status'] ?? '').toString();
  final issued = c['direction'] == 'issued';
  return ListTile(
    onTap: () => pushPage(context, ChequeDetailScreen(id: c['id'].toString())),
    leading: Icon(issued ? Icons.north_east : Icons.south_west),
    title: Text('${c['number']} · ${c['instrumentNo']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${c['partyName'] ?? ''}\n${issued ? 'Issued' : 'Received'} · ${formatDate(c['instrumentDate'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(c, 'amount'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class ChequesScreen extends StatelessWidget {
  const ChequesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Cheque register',
        path: chequesPath,
        searchHint: 'Search cheques',
        emptyText: 'No cheques found.',
        itemBuilder: chequeTile,
        filters: chequeStatuses,
      );
}

FormSpec clearIssuedForm(Json c) => FormSpec(
      title: 'Clear cheque',
      intro: 'Cheque ${c['instrumentNo']} has cleared the bank on:',
      fields: [Field.date('date', 'Clearing date', required: true, initial: 'today', validator: plausibleDate)],
      submitLabel: 'Mark cleared',
      submit: (ref, body, raw) => ref.read(apiProvider).post('$chequesPath/${c['id']}/clear-issued', body: body),
    );

class ChequeDetailScreen extends StatelessWidget {
  const ChequeDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$chequesPath/$id',
        titleOf: (d) => d['number'].toString(),
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: d['partyName']?.toString() ?? '', subtitle: '${humanize(d['kind']?.toString())} · ${humanize(d['direction']?.toString())}', status: d['status']?.toString()),
          InfoCard(rows: [
            InfoRow('Cheque number', d['instrumentNo']?.toString()),
            InfoRow('Bank', d['bankName']?.toString()),
            InfoRow('Amount', money(d, 'amount'), bold: true),
            InfoRow('Cheque date', formatDate(d['instrumentDate'])),
            InfoRow('Received on', d['receivedOn'] == null ? null : formatDate(d['receivedOn'])),
            InfoRow('Deposited on', d['depositedOn'] == null ? null : formatDate(d['depositedOn'])),
            InfoRow('Cleared on', d['clearedOn'] == null ? null : formatDate(d['clearedOn'])),
            InfoRow('Bounced on', d['bouncedOn'] == null ? null : formatDate(d['bouncedOn'])),
            InfoRow('Bounce reason', d['bounceReason']?.toString()),
            InfoRow('Follow-up note', d['followUpNote']?.toString()),
            InfoRow('Notes', d['notes']?.toString()),
          ]),
        ]),
        actions: [
          ActionSpec(
            label: 'Clear',
            icon: Icons.account_balance,
            permission: 'finance.cheques.approve',
            visible: (d) => d['direction'] == 'issued' && d['status'] == 'issued',
            form: clearIssuedForm,
            run: (ref, d, _) async => null,
          ),
        ],
      );
}

// ---------------------------------------------------------------- cash and bank

const bankPath = '$finPath/bank';

FormSpec transferForm(WidgetRef ref) => FormSpec(
      title: 'Transfer between accounts',
      fields: [
        entityField(ref),
        accountField('fromAccountId', 'From account', bankOnly: true),
        accountField('toAccountId', 'To account', bankOnly: true),
        Field.money('amount', 'Amount', required: true, min: 0.01),
        Field.date('date', 'Date', required: true, initial: 'today', validator: plausibleDate),
        Field.text('memo', 'Memo', maxLength: 300),
      ],
      submit: (ref, body, raw) async {
        if (body['fromAccountId'] == body['toAccountId']) throw ApiException(422, 'Choose two different accounts.');
        return ref.read(apiProvider).post('$bankPath/transfers', body: body);
      },
    );

class BankScreen extends ConsumerStatefulWidget {
  const BankScreen({super.key});

  @override
  ConsumerState<BankScreen> createState() => _BankScreenState();
}

class _BankScreenState extends ConsumerState<BankScreen> {
  final String _asOf = todayIso();

  @override
  Widget build(BuildContext context) {
    final canWrite = ref.watch(meOrEmptyProvider).can('finance.bank.write');
    return Scaffold(
      appBar: AppBar(title: const Text('Cash and bank')),
      floatingActionButton: canWrite ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, transferForm(ref)), icon: const Icon(Icons.swap_horiz), label: const Text('Transfer')) : null,
      body: ApiView(
        path: '$bankPath/position',
        query: {'asOf': _asOf},
        builder: (context, data) {
          final d = data as Json;
          final accounts = (d['accounts'] as List<dynamic>).cast<Json>();
          final currency = ref.read(meOrEmptyProvider).baseCurrency;
          return DetailBody(children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Total cash and bank', style: Theme.of(context).textTheme.titleSmall),
                  Text('$currency ${formatMoney((d['total'] as num?) ?? 0)}', style: Theme.of(context).textTheme.headlineSmall),
                  Text('As of ${formatDate(_asOf)}'),
                ]),
              ),
            ),
            const SizedBox(height: 8),
            for (final a in accounts)
              ListTile(
                leading: Icon(a['isBank'] == true ? Icons.account_balance_outlined : Icons.payments_outlined),
                title: Text('${a['name']}'),
                subtitle: Text('${a['code']} · ${a['isBank'] == true ? 'Bank' : 'Cash'}'),
                trailing: Text(formatMoney((a['balance'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
              ),
          ]);
        },
      ),
    );
  }
}

// ---------------------------------------------------------------- budgets

const budgetsPath = '$finPath/budgets';

Widget budgetTile(BuildContext context, Json b) => ListTile(
      onTap: () => pushPage(context, BudgetScreen(id: b['id'].toString(), title: b['name'].toString())),
      title: Text('${b['name']}'),
      subtitle: Text('Fiscal year ${b['fiscalYear']}'),
      trailing: StatusChip(b['isActive'] == true ? 'Active' : 'Inactive', color: StatusChip.forStatus(b['isActive'] == true ? 'active' : 'draft')),
    );

class BudgetsScreen extends StatelessWidget {
  const BudgetsScreen({super.key});

  @override
  Widget build(BuildContext context) => const ListScreen(title: 'Budgets', path: budgetsPath, itemBuilder: budgetTile, searchHint: 'Search budgets', emptyText: 'No budgets found.');
}

const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/// Budget against actual for a range of months (view only).
class BudgetScreen extends StatefulWidget {
  const BudgetScreen({super.key, required this.id, required this.title});

  final String id;
  final String title;

  @override
  State<BudgetScreen> createState() => _BudgetScreenState();
}

class _BudgetScreenState extends State<BudgetScreen> {
  int _from = 1;
  int _to = 12;

  Widget _month(String label, int value, ValueChanged<int> onChanged) => Expanded(
        child: DropdownButtonFormField<int>(
          initialValue: value,
          decoration: InputDecoration(labelText: label, border: const OutlineInputBorder(), isDense: true),
          items: [for (var m = 1; m <= 12; m++) DropdownMenuItem(value: m, child: Text(_months[m - 1]))],
          onChanged: (v) => v == null ? null : onChanged(v),
        ),
      );

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(widget.title)),
        body: ApiView(
          path: '$budgetsPath/${widget.id}/vs-actual',
          query: {'fromMonth': _from, 'toMonth': _to},
          builder: (context, data) {
            final d = data as Json;
            final rows = (d['rows'] as List<dynamic>).cast<Json>();
            return DetailBody(children: [
              Row(children: [
                _month('From', _from, (v) => setState(() {
                      _from = v;
                      if (_to < v) _to = v;
                    })),
                const SizedBox(width: 12),
                _month('To', _to, (v) => setState(() {
                      _to = v;
                      if (_from > v) _from = v;
                    })),
              ]),
              const SizedBox(height: 12),
              InfoCard(title: 'Fiscal year ${d['fiscalYear']}', rows: [
                InfoRow('Income: budget', formatMoney((d['budgetIncome'] as num?) ?? 0)),
                InfoRow('Income: actual', formatMoney((d['actualIncome'] as num?) ?? 0)),
                InfoRow('Expense: budget', formatMoney((d['budgetExpense'] as num?) ?? 0)),
                InfoRow('Expense: actual', formatMoney((d['actualExpense'] as num?) ?? 0)),
                InfoRow('Net: budget', formatMoney((d['budgetNet'] as num?) ?? 0), bold: true),
                InfoRow('Net: actual', formatMoney((d['actualNet'] as num?) ?? 0), bold: true),
              ]),
              for (final r in rows)
                Card(
                  child: ListTile(
                    title: Text('${r['code']} ${r['name']}'),
                    subtitle: Text('Budget ${formatMoney((r['budget'] as num?) ?? 0)} · Actual ${formatMoney((r['actual'] as num?) ?? 0)}'),
                    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
                      Text(formatMoney((r['variance'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
                      if (r['variancePercent'] != null) Text('${formatMoney((r['variancePercent'] as num))}%'),
                    ]),
                  ),
                ),
            ]);
          },
        ),
      );
}

// ---------------------------------------------------------------- chart of accounts

const accountsPath = '$finPath/accounts';
const accountTypes = [Option('asset', 'Asset'), Option('liability', 'Liability'), Option('equity', 'Equity'), Option('income', 'Income'), Option('expense', 'Expense')];

FormSpec accountForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New account',
    initial: {
      if (editing) ...{
        'code': existing['code'],
        'name': existing['name'],
        'type': existing['type'],
        'reportingGroup': existing['reportingGroup'],
        'parentId': existing['parentId'],
        if (existing['parentId'] != null) '__label_parentId': 'Parent account',
        'isPostable': existing['isPostable'],
        'isBank': existing['isBank'],
        'isActive': existing['isActive'],
      } else
        ...{'isPostable': true, 'isActive': true},
    },
    fields: [
      Field.text('code', 'Code', required: true, maxLength: 20),
      Field.text('name', 'Name', required: true, maxLength: 200),
      Field.select('type', 'Type', options: accountTypes, required: true),
      Field.text('reportingGroup', 'Reporting group', maxLength: 100),
      Field.lookup('parentId', 'Parent account (optional)', path: accountsPath, subtitleKey: 'code'),
      Field.toggle('isPostable', 'Can be posted to', initial: true),
      Field.toggle('isBank', 'Bank or cash account'),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$accountsPath/${existing['id']}', body: body) : api.post(accountsPath, body: body);
    },
  );
}

Widget accountTile(BuildContext context, Json a) => ListTile(
      onTap: () => pushPage(context, AccountDetailScreen(id: a['id'].toString())),
      leading: CircleAvatar(child: Text('${a['type'] ?? '?'}'.substring(0, 1).toUpperCase())),
      title: Text('${a['code']} · ${a['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${humanize(a['type']?.toString())}${(a['reportingGroup'] ?? '').toString().isEmpty ? '' : ' · ${a['reportingGroup']}'}'),
      trailing: a['isActive'] == true ? null : StatusChip('Inactive', color: StatusChip.forStatus('draft')),
    );

class AccountsScreen extends StatelessWidget {
  const AccountsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Chart of accounts',
        path: accountsPath,
        searchHint: 'Search accounts',
        emptyText: 'No accounts found.',
        itemBuilder: accountTile,
        filters: accountTypes,
        filterKey: 'type',
        create: (ref) => accountForm(ref),
        createPermission: 'finance.accounts.write',
      );
}

class AccountDetailScreen extends StatelessWidget {
  const AccountDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(
        builder: (context, ref, _) => DetailPage(
          path: '$accountsPath/$id',
          titleOf: (d) => '${d['code']} ${d['name']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('finance.accounts.write'),
          edit: (d) => accountForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: d['name'].toString(), subtitle: '${d['code']} · ${humanize(d['type']?.toString())}', status: d['isActive'] == true ? 'active' : 'inactive'),
            InfoCard(rows: [
              InfoRow('Reporting group', d['reportingGroup']?.toString()),
              InfoRow('Posting', d['isPostable'] == true ? 'Postable' : 'Group (no postings)'),
              InfoRow('Bank or cash', d['isBank'] == true ? 'Yes' : 'No'),
              InfoRow('System account', d['isSystem'] == true ? 'Yes' : null),
              InfoRow('Currency', d['currencyCode']?.toString()),
            ]),
          ]),
        ),
      );
}

// ---------------------------------------------------------------- tax codes

const taxCodesPath = '$finPath/tax-codes';
const taxKinds = [Option('vat', 'VAT'), Option('gst', 'GST'), Option('salesTax', 'Sales tax'), Option('withholding', 'Withholding'), Option('excise', 'Excise'), Option('other', 'Other')];

FormSpec taxCodeForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['code']}' : 'New tax code',
    initial: {
      if (editing) ...{
        'code': existing['code'],
        'name': existing['name'],
        'kind': existing['kind'],
        'rate': existing['rate'],
        'isRecoverable': existing['isRecoverable'],
        'isDefault': existing['isDefault'],
        'isActive': existing['isActive'],
      },
    },
    fields: [
      Field.text('code', 'Code', required: true, maxLength: 20),
      Field.text('name', 'Name', required: true, maxLength: 100),
      Field.select('kind', 'Kind', options: taxKinds, required: true, initial: 'vat'),
      Field.decimal('rate', 'Rate (%)', required: true, min: 0, max: 100, decimals: 4),
      Field.toggle('isRecoverable', 'Recoverable (input tax)', initial: true),
      Field.toggle('isDefault', 'Default tax code'),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$taxCodesPath/${existing['id']}', body: body) : api.post(taxCodesPath, body: body);
    },
  );
}

class TaxCodesScreen extends ConsumerWidget {
  const TaxCodesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canWrite = ref.watch(meOrEmptyProvider).can('finance.tax.write');
    return ListScreen(
      title: 'Tax codes',
      path: taxCodesPath,
      searchHint: 'Search tax codes',
      emptyText: 'No tax codes found.',
      create: (ref) => taxCodeForm(ref),
      createPermission: 'finance.tax.write',
      itemBuilder: (context, t) => ListTile(
        onTap: canWrite ? () => openForm<Object?>(context, taxCodeForm(ref, existing: t)) : null,
        title: Text('${t['code']} · ${t['name']}'),
        subtitle: Text('${humanize(t['kind']?.toString())}${t['isDefault'] == true ? ' · Default' : ''}${t['isRecoverable'] == true ? ' · Recoverable' : ''}'),
        trailing: Text('${formatMoney((t['rate'] as num?) ?? 0)}%', style: const TextStyle(fontWeight: FontWeight.w600)),
      ),
    );
  }
}
