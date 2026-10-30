import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

typedef _Json = Map<String, dynamic>;

Future<DateTime?> _pickDate(BuildContext context, DateTime current) =>
    showDatePicker(context: context, initialDate: current, firstDate: DateTime(2000), lastDate: DateTime.now().add(const Duration(days: 366)));

/// Shared shell: loads [path] with `asOf`, offers a date picker, and draws [builder] on the answer.
class _AsOfReport extends ConsumerStatefulWidget {
  const _AsOfReport({required this.title, required this.path, required this.builder, this.query = const {}});

  final String title;
  final String path;
  final Map<String, dynamic> query;
  final Widget Function(BuildContext context, _Json data) builder;

  @override
  ConsumerState<_AsOfReport> createState() => _AsOfReportState();
}

class _AsOfReportState extends ConsumerState<_AsOfReport> {
  DateTime _asOf = DateTime.now();
  _Json? _data;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final d = await ref.read(apiProvider).get(widget.path, query: {...widget.query, 'asOf': todayIsoOf(_asOf)});
      if (mounted) setState(() => _data = d as _Json);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(widget.title), actions: [
          TextButton.icon(
            onPressed: () async {
              final d = await _pickDate(context, _asOf);
              if (d != null) {
                _asOf = d;
                _load();
              }
            },
            icon: const Icon(Icons.event),
            label: Text(formatDate(todayIsoOf(_asOf))),
          ),
        ]),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? ErrorView(message: _error!, onRetry: _load)
                : RefreshIndicator(onRefresh: _load, child: widget.builder(context, _data!)),
      );
}

String todayIsoOf(DateTime d) => '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

const _buckets = [('current', 'Current'), ('days1To30', '1-30 days'), ('days31To60', '31-60 days'), ('days61To90', '61-90 days'), ('over90', 'Over 90 days')];

class ArAgeingScreen extends StatelessWidget {
  const ArAgeingScreen({super.key});

  @override
  Widget build(BuildContext context) => _AsOfReport(
        title: 'Receivables ageing',
        path: '/api/v1/finance/reports/ar-ageing',
        builder: (context, d) {
          final rows = (d['rows'] as List<dynamic>? ?? const []).cast<_Json>();
          final totals = (d['totals'] as _Json?) ?? const {};
          return ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(16), children: [
            InfoCard(title: 'Total receivable', rows: [
              for (final b in _buckets) InfoRow(b.$2, formatMoney((totals[b.$1] as num?) ?? 0)),
              InfoRow('Total', formatMoney((totals['total'] as num?) ?? 0), bold: true),
            ]),
            if (rows.isEmpty) const Padding(padding: EdgeInsets.all(24), child: Center(child: Text('Nothing is owed.'))),
            for (final r in rows)
              Card(
                child: ExpansionTile(
                  title: Text(r['partyName']?.toString() ?? '', maxLines: 1, overflow: TextOverflow.ellipsis),
                  trailing: Text(formatMoney((r['total'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
                  childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                  children: [
                    for (final b in _buckets) InfoRow(b.$2, formatMoney((r[b.$1] as num?) ?? 0)),
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton(
                        onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CustomerStatementScreen(partyId: r['partyId'].toString(), partyName: r['partyName']?.toString() ?? ''))),
                        child: const Text('Statement'),
                      ),
                    ),
                  ],
                ),
              ),
          ]);
        },
      );
}

class CustomerStatementScreen extends StatelessWidget {
  const CustomerStatementScreen({super.key, required this.partyId, required this.partyName});

  final String partyId;
  final String partyName;

  @override
  Widget build(BuildContext context) => _AsOfReport(
        title: 'Statement',
        path: '/api/v1/finance/reports/customer-statement',
        query: {'partyId': partyId},
        builder: (context, d) {
          final items = (d['items'] as List<dynamic>? ?? const []).cast<_Json>();
          return ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(16), children: [
            Text(d['partyName']?.toString() ?? partyName, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Unallocated receipts', formatMoney((d['unallocatedReceipts'] as num?) ?? 0)),
              InfoRow('Balance', formatMoney((d['balance'] as num?) ?? 0), bold: true),
            ]),
            Text('Open items', style: Theme.of(context).textTheme.titleMedium),
            if (items.isEmpty) const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No open items.')),
            for (final i in items)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${i['isCreditNote'] == true ? 'Credit note ' : ''}${i['number']}'),
                subtitle: Text('Due ${formatDate(i['dueDate'])}${((i['daysOverdue'] as num?) ?? 0) > 0 ? ' · ${i['daysOverdue']} days overdue' : ''}'),
                trailing: Text(formatMoney((i['outstanding'] as num?) ?? 0)),
              ),
          ]);
        },
      );
}
