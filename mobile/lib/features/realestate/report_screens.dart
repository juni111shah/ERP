import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/widgets.dart';
import 'common.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';
import '../../kit/state_view.dart';

const _reportsPath = '$rePath/reports';

/// Reports a property manager reads on the go. Reached from the Properties screen (the module menu has no entry for reports).
class ReportsScreen extends StatelessWidget {
  const ReportsScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Reports')),
        body: ListView(children: [
          ListTile(leading: const Icon(Icons.list_alt), title: const Text('Rent roll'), subtitle: const Text('Active leases, rent and amounts outstanding'), onTap: () => reGo(context, const RentRollScreen())),
          ListTile(leading: const Icon(Icons.pie_chart_outline), title: const Text('Occupancy'), subtitle: const Text('Units occupied and vacant per property'), onTap: () => reGo(context, const OccupancyScreen())),
          ListTile(leading: const Icon(Icons.event_busy_outlined), title: const Text('Lease expiries'), subtitle: const Text('Rent at risk by month'), onTap: () => reGo(context, const ExpiriesScreen())),
          ListTile(leading: const Icon(Icons.receipt_long_outlined), title: const Text('Owner statement'), subtitle: const Text('Rent, fees and maintenance for an owner'), onTap: () => reGo(context, const OwnerStatementScreen())),
        ]),
      );
}

/// Loads a report from [path] and draws it with [builder].
class _Report extends ConsumerStatefulWidget {
  const _Report({required this.title, required this.path, required this.builder, this.query});

  final String title;
  final String path;
  final Map<String, dynamic>? query;
  final List<Widget> Function(BuildContext context, Json data) builder;

  @override
  ConsumerState<_Report> createState() => _ReportState();
}

class _ReportState extends ConsumerState<_Report> {
  Json? _data;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final d = await ref.read(apiProvider).get(widget.path, query: widget.query);
      if (mounted) setState(() => _data = (d as Map).cast<String, dynamic>());
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(widget.title)),
        body: _data == null
            ? (_error == null ? const ErpSkeleton.list() : ErpStateView.error(message: _error!, onRetry: _load))
            : RefreshIndicator(onRefresh: _load, child: ListView(padding: const EdgeInsets.all(ErpSpace.s4), children: widget.builder(context, _data!))),
      );
}

List<Json> _rows(Json d, [String key = 'rows']) => (d[key] as List<dynamic>? ?? const []).cast<Json>();
String _m(Object? v) => formatMoney((v as num?) ?? 0);

class RentRollScreen extends StatelessWidget {
  const RentRollScreen({super.key});

  @override
  Widget build(BuildContext context) => _Report(
        title: 'Rent roll',
        path: '$_reportsPath/rent-roll',
        query: {'asOf': todayIso()},
        builder: (context, d) => [
          InfoCard(rows: [InfoRow('Total annual rent', _m(d['totalAnnualRent']), bold: true), InfoRow('Total outstanding', _m(d['totalOutstanding']), bold: true)]),
          if (_rows(d).isEmpty) const Text('No active leases.'),
          for (final r in _rows(d))
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text('${r['tenant']}'),
              subtitle: Text('${r['property']} · Unit ${r['unit']}\nExpires ${formatDate(r['expiry'])} (${r['daysLeft']} days)'),
              isThreeLine: true,
              trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
                Text(_m(r['annualRent']), style: const TextStyle(fontWeight: FontWeight.w600)),
                if (((r['outstanding'] as num?) ?? 0) > 0) Text('Owes ${_m(r['outstanding'])}', style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ]),
            ),
        ],
      );
}

class OccupancyScreen extends StatelessWidget {
  const OccupancyScreen({super.key});

  @override
  Widget build(BuildContext context) => _Report(
        title: 'Occupancy',
        path: '$_reportsPath/occupancy',
        builder: (context, d) => [
          InfoCard(rows: [
            InfoRow('Occupancy', '${_m(d['occupancyPercent'])}%', bold: true),
            InfoRow('Vacant units', '${d['vacantUnits'] ?? 0}'),
            InfoRow('Contracted rent', _m(d['contractedRent'])),
            InfoRow('Vacant asking rent', _m(d['vacancyAskingRent'])),
          ]),
          for (final r in _rows(d))
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text('${r['property']}'),
              subtitle: Text('${r['occupied']} occupied · ${r['vacant']} vacant · ${r['registered']} of ${r['totalUnits']} registered'),
              trailing: Text('${_m(r['occupancyPercent'])}%', style: const TextStyle(fontWeight: FontWeight.w600)),
            ),
        ],
      );
}

class ExpiriesScreen extends StatelessWidget {
  const ExpiriesScreen({super.key});

  @override
  Widget build(BuildContext context) => _Report(
        title: 'Lease expiries',
        path: '$_reportsPath/lease-expiries',
        query: const {'months': 12},
        builder: (context, d) => [
          InfoCard(rows: [InfoRow('Leases expiring', '${d['totalLeases'] ?? 0}', bold: true), InfoRow('Rent at risk', _m(d['totalRentAtRisk']))]),
          for (final r in _rows(d))
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text('${r['label']}'),
              subtitle: Text('${r['leases']} leases${r['pastEnd'] == true ? ' · past end date' : ''}'),
              trailing: Text(_m(r['annualRentAtRisk'])),
            ),
        ],
      );
}

class OwnerStatementScreen extends ConsumerStatefulWidget {
  const OwnerStatementScreen({super.key});

  @override
  ConsumerState<OwnerStatementScreen> createState() => _OwnerStatementState();
}

class _OwnerStatementState extends ConsumerState<OwnerStatementScreen> {
  Json? _data;

  FormSpec _spec() => FormSpec(
        title: 'Owner statement',
        submitLabel: 'Show statement',
        fields: [
          Field.lookup('ownerPartyId', 'Owner', path: '/api/v1/parties', subtitleKey: 'code', required: true),
          Field.date('from', 'From', required: true, initial: '${DateTime.now().year}-01-01'),
          Field.date('to', 'To', required: true, initial: 'today'),
        ],
        submit: (ref, body, raw) => ref.read(apiProvider).get('$_reportsPath/owner-statement', query: body),
      );

  @override
  Widget build(BuildContext context) {
    final d = _data;
    return Scaffold(
      appBar: AppBar(title: const Text('Owner statement')),
      body: ListView(padding: const EdgeInsets.all(ErpSpace.s4), children: [
        FilledButton.icon(
          icon: const Icon(Icons.search),
          label: Text(d == null ? 'Choose owner and period' : 'Change owner or period'),
          onPressed: () async {
            final res = await openForm<Object?>(context, _spec());
            if (res is Map && mounted) setState(() => _data = res.cast<String, dynamic>());
          },
        ),
        if (d != null) ...[
          const SizedBox(height: ErpSpace.s3),
          DetailHeader(title: '${d['owner']}', subtitle: '${formatDate(d['from'])} to ${formatDate(d['to'])}'),
          InfoCard(rows: [
            InfoRow('Rent collected', _m(d['rentCollected'])),
            InfoRow('Management fee', _m(d['managementFee'])),
            InfoRow('Maintenance costs', _m(d['maintenanceCosts'])),
            InfoRow('Net to owner', _m(d['netToOwner']), bold: true),
          ]),
          for (final r in _rows(d))
            InfoCard(title: '${r['property']}', rows: [
              InfoRow('Rent collected', _m(r['rentCollected'])),
              InfoRow('Fee (${r['feePercent']}%)', _m(r['managementFee'])),
              InfoRow('Maintenance', _m(r['maintenanceCosts'])),
              InfoRow('Net', _m(r['netToOwner']), bold: true),
            ]),
        ],
      ]),
    );
  }
}
