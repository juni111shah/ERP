import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/widgets.dart';
import 'api_view.dart';
import 'common.dart';

enum ReportKind { apAgeing, arAgeing, trialBalance, incomeStatement, balanceSheet, cashPosition }

class ReportSpec {
  const ReportSpec(this.kind, this.title, this.subtitle, this.icon, this.path, {required this.range});

  final ReportKind kind;
  final String title;
  final String subtitle;
  final IconData icon;
  final String path;

  /// true: needs from/to dates; false: a single "as of" date.
  final bool range;
}

const reportSpecs = [
  ReportSpec(ReportKind.apAgeing, 'Payables ageing', 'What we owe vendors, by days overdue', Icons.call_made, '$finPath/reports/ap-ageing', range: false),
  ReportSpec(ReportKind.arAgeing, 'Receivables ageing', 'What customers owe us, by days overdue', Icons.call_received, '$finPath/reports/ar-ageing', range: false),
  ReportSpec(ReportKind.trialBalance, 'Trial balance', 'Opening, movement and closing per account', Icons.balance, '$finPath/reports/trial-balance', range: true),
  ReportSpec(ReportKind.incomeStatement, 'Profit and loss', 'Income and expenses for a period', Icons.trending_up, '$finPath/reports/income-statement', range: true),
  ReportSpec(ReportKind.balanceSheet, 'Balance sheet', 'Assets, liabilities and equity', Icons.account_balance_wallet_outlined, '$finPath/reports/balance-sheet', range: false),
  ReportSpec(ReportKind.cashPosition, 'Cash position', 'Balances of bank and cash accounts', Icons.savings_outlined, '$finPath/bank/position', range: false),
];

class ReportsScreen extends StatelessWidget {
  const ReportsScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Reports')),
        body: ListView(children: [
          for (final r in reportSpecs)
            ListTile(
              leading: Icon(r.icon),
              title: Text(r.title),
              subtitle: Text(r.subtitle),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => pushPage(context, ReportPage(spec: r)),
            ),
        ]),
      );
}

String _iso(DateTime d) => DateFormat('yyyy-MM-dd').format(d);

class ReportPage extends ConsumerStatefulWidget {
  const ReportPage({super.key, required this.spec});

  final ReportSpec spec;

  @override
  ConsumerState<ReportPage> createState() => _ReportPageState();
}

class _ReportPageState extends ConsumerState<ReportPage> {
  late String _to = todayIso();
  late String _from = _iso(DateTime(DateTime.now().year, 1, 1));
  String? _entity;

  Future<void> _pick(bool from) async {
    final current = DateTime.tryParse(from ? _from : _to) ?? DateTime.now();
    final picked = await showDatePicker(context: context, initialDate: current, firstDate: DateTime(2000), lastDate: DateTime.now().add(const Duration(days: 365)));
    if (picked == null) return;
    setState(() => from ? _from = _iso(picked) : _to = _iso(picked));
  }

  @override
  Widget build(BuildContext context) {
    final spec = widget.spec;
    final me = ref.watch(meOrEmptyProvider);
    final currency = me.baseCurrency;
    final query = <String, dynamic>{
      if (spec.range) 'from': _from,
      spec.range ? 'to' : 'asOf': _to,
      if (_entity != null) 'legalEntityId': _entity,
    };
    final bad = spec.range && _from.compareTo(_to) > 0;
    return Scaffold(
      appBar: AppBar(title: Text(spec.title)),
      body: Column(children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
          child: Wrap(spacing: 8, runSpacing: 4, crossAxisAlignment: WrapCrossAlignment.center, children: [
            if (spec.range) ActionChip(avatar: const Icon(Icons.event, size: 18), label: Text('From ${formatDate(_from)}'), onPressed: () => _pick(true)),
            ActionChip(avatar: const Icon(Icons.event, size: 18), label: Text('${spec.range ? 'To' : 'As of'} ${formatDate(_to)}'), onPressed: () => _pick(false)),
            if (me.legalEntities.length > 1)
              DropdownButton<String?>(
                value: _entity,
                hint: const Text('All entities'),
                items: [const DropdownMenuItem(value: null, child: Text('All entities')), for (final e in me.legalEntities) DropdownMenuItem(value: e.id, child: Text(e.name))],
                onChanged: (v) => setState(() => _entity = v),
              ),
          ]),
        ),
        if (bad) Padding(padding: const EdgeInsets.all(12), child: Text('The start date is after the end date.', style: TextStyle(color: Theme.of(context).colorScheme.error))),
        if (!bad)
          Expanded(
            child: ApiView(
              path: spec.path,
              query: query,
              builder: (context, data) => reportBody(context, spec.kind, data as Json, currency),
            ),
          ),
      ]),
    );
  }
}

Widget reportBody(BuildContext context, ReportKind kind, Json d, String currency) => switch (kind) {
      ReportKind.apAgeing || ReportKind.arAgeing => ageingBody(context, d),
      ReportKind.trialBalance => trialBalanceBody(context, d),
      ReportKind.incomeStatement => incomeBody(context, d),
      ReportKind.balanceSheet => balanceSheetBody(context, d),
      ReportKind.cashPosition => cashBody(context, d, currency),
    };

String _m(Object? v) => formatMoney((v as num?) ?? 0);

Widget ageingBody(BuildContext context, Json d) {
  final rows = (d['rows'] as List<dynamic>? ?? const []).cast<Json>();
  Widget card(Json r, {bool total = false}) => Card(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(total ? 'Total' : (r['partyName'] ?? 'Unknown').toString(), style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 4),
            InfoRow('Current', _m(r['current'])),
            InfoRow('1-30 days', _m(r['days1To30'])),
            InfoRow('31-60 days', _m(r['days31To60'])),
            InfoRow('61-90 days', _m(r['days61To90'])),
            InfoRow('Over 90 days', _m(r['over90'])),
            InfoRow('Total', _m(r['total']), bold: true),
          ]),
        ),
      );
  return DetailBody(children: [
    if (d['totals'] != null) card(d['totals'] as Json, total: true),
    if (rows.isEmpty) const Padding(padding: EdgeInsets.all(24), child: Center(child: Text('Nothing outstanding.'))),
    for (final r in rows) card(r),
  ]);
}

Widget trialBalanceBody(BuildContext context, Json d) {
  final rows = (d['rows'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    InfoCard(rows: [
      InfoRow('Total debit', _m(d['totalDebit'])),
      InfoRow('Total credit', _m(d['totalCredit'])),
      InfoRow('Balanced', d['balanced'] == true ? 'Yes' : 'No', bold: true),
    ]),
    for (final r in rows)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${r['code']} ${r['name']}'),
        subtitle: Text('Opening ${_m(r['opening'])} · Dr ${_m(r['debit'])} · Cr ${_m(r['credit'])}'),
        trailing: Text(_m(r['closing']), style: const TextStyle(fontWeight: FontWeight.w600)),
      ),
  ]);
}

List<Widget> _statementSection(BuildContext context, String title, Object? lines, Object? total) {
  final rows = (lines as List<dynamic>? ?? const []).cast<Json>();
  return [
    Padding(padding: const EdgeInsets.only(top: 8), child: Text(title, style: Theme.of(context).textTheme.titleMedium)),
    for (final l in rows)
      ListTile(
        dense: true,
        contentPadding: EdgeInsets.zero,
        title: Text('${l['code']} ${l['name']}'),
        subtitle: Text('${l['group'] ?? ''}'),
        trailing: Text(_m(l['amount'])),
      ),
    InfoRow('Total $title'.toLowerCase().replaceFirst('t', 'T'), _m(total), bold: true),
  ];
}

Widget incomeBody(BuildContext context, Json d) => DetailBody(children: [
      InfoCard(rows: [InfoRow('Net profit', _m(d['netProfit']), bold: true)]),
      ..._statementSection(context, 'Income', d['income'], d['totalIncome']),
      ..._statementSection(context, 'Expenses', d['expenses'], d['totalExpenses']),
    ]);

Widget balanceSheetBody(BuildContext context, Json d) => DetailBody(children: [
      InfoCard(rows: [
        InfoRow('Total assets', _m(d['totalAssets']), bold: true),
        InfoRow('Total liabilities', _m(d['totalLiabilities'])),
        InfoRow('Total equity', _m(d['totalEquity'])),
        InfoRow('Current earnings', _m(d['currentEarnings'])),
        InfoRow('Balanced', d['balanced'] == true ? 'Yes' : 'No'),
      ]),
      ..._statementSection(context, 'Assets', d['assets'], d['totalAssets']),
      ..._statementSection(context, 'Liabilities', d['liabilities'], d['totalLiabilities']),
      ..._statementSection(context, 'Equity', d['equity'], d['totalEquity']),
    ]);

Widget cashBody(BuildContext context, Json d, String currency) {
  final accounts = (d['accounts'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    InfoCard(rows: [InfoRow('Total cash and bank', '$currency ${_m(d['total'])}'.trim(), bold: true)]),
    for (final a in accounts)
      ListTile(
        contentPadding: EdgeInsets.zero,
        leading: Icon(a['isBank'] == true ? Icons.account_balance_outlined : Icons.payments_outlined),
        title: Text('${a['name']}'),
        subtitle: Text('${a['code']}'),
        trailing: Text(_m(a['balance']), style: const TextStyle(fontWeight: FontWeight.w600)),
      ),
  ]);
}
