import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/widgets.dart';
import 'hr_common.dart';
import 'payroll_screens.dart';
import 'package:erp_mobile/theme/tokens.dart';

String _iso(DateTime d) => '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// End an employment: choose the dates, look at what the leaver is owed, confirm, then prepare the final-settlement payroll run.
class SettlementScreen extends ConsumerStatefulWidget {
  const SettlementScreen({super.key, required this.employeeId, required this.name, this.terminated = false, this.terminationDate});

  final String employeeId;
  final String name;

  /// The employee has already left: only the preview and the final settlement are offered.
  final bool terminated;
  final String? terminationDate;

  @override
  ConsumerState<SettlementScreen> createState() => _SettlementState();
}

class _SettlementState extends ConsumerState<SettlementScreen> {
  DateTime? _termination;
  DateTime? _lastDay;
  DateTime? _notice;
  final _reason = TextEditingController();
  Json? _preview;
  bool _busy = false;
  bool _ended = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _ended = widget.terminated;
    if (widget.terminationDate != null) _termination = DateTime.tryParse(widget.terminationDate!);
    if (_ended && _termination != null) WidgetsBinding.instance.addPostFrameCallback((_) => _loadPreview());
  }

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<DateTime?> _pick(DateTime? initial) =>
      showDatePicker(context: context, initialDate: initial ?? DateTime.now(), firstDate: DateTime(2000), lastDate: DateTime(2100));

  Future<void> _run(Future<void> Function() job) async {
    setState(() { _busy = true; _error = null; });
    try {
      await job();
    } on ApiException catch (e) {
      _error = e.message;
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _loadPreview() => _run(() async {
        final data = await ref.read(apiProvider).get('$hrPath/employees/${widget.employeeId}/settlement-preview',
            query: {'terminationDate': _iso(_termination!), if (_notice != null) 'noticeGivenOn': _iso(_notice!)});
        _preview = data as Json;
      });

  Future<void> _end() => _run(() async {
        final data = await ref.read(apiProvider).post('$hrPath/employees/${widget.employeeId}/terminate', body: {
          'terminationDate': _iso(_termination!),
          'reason': _reason.text.isEmpty ? null : _reason.text,
          'lastWorkingDay': _lastDay == null ? null : _iso(_lastDay!),
          'noticeGivenOn': _notice == null ? null : _iso(_notice!),
        });
        _preview = data as Json;
        _ended = true;
        ref.read(dataVersionProvider.notifier).bump();
      });

  Future<void> _settle() => _run(() async {
        final run = await ref.read(apiProvider).post('$hrPath/employees/${widget.employeeId}/final-settlement') as Json;
        ref.read(dataVersionProvider.notifier).bump();
        if (!mounted) return;
        await Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => PayrollDetailScreen(id: '${run['id']}')));
      });

  Widget _dateTile(String label, DateTime? value, ValueChanged<DateTime?> onPicked, {bool enabled = true}) => ListTile(
        enabled: enabled,
        title: Text(label),
        trailing: Text(value == null ? 'Choose' : formatDate(_iso(value))),
        onTap: !enabled ? null : () async {
          final d = await _pick(value);
          if (d != null) setState(() { onPicked(d); _preview = _ended ? _preview : null; });
        },
      );

  @override
  Widget build(BuildContext context) {
    final p = _preview;
    return Scaffold(
      appBar: AppBar(title: Text(_ended ? 'Final settlement · ${widget.name}' : 'End employment · ${widget.name}')),
      body: ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
        if (!_ended) ...[
          _dateTile('Termination date', _termination, (d) => _termination = d),
          _dateTile('Last working day', _lastDay, (d) => _lastDay = d),
          _dateTile('Notice given on', _notice, (d) => _notice = d),
          Padding(padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s3), child: TextField(controller: _reason, maxLength: 300, decoration: const InputDecoration(labelText: 'Reason'))),
        ],
        if (p != null)
          InfoCard(title: 'Settlement', rows: [
            InfoRow('Salary for ${monthName((p['month'] as num).toInt())} ${p['year']}', formatMoney(((p['finalBasic'] as num?) ?? 0) + ((p['finalAllowances'] as num?) ?? 0))),
            InfoRow('Unused leave (${days(p['leaveDays'])} days)', formatMoney((p['leaveEncashment'] as num?) ?? 0)),
            InfoRow('End of service', formatMoney((p['endOfService'] as num?) ?? 0)),
            InfoRow('Gross', formatMoney((p['gross'] as num?) ?? 0), bold: true),
            if (((p['recurringDeductions'] as num?) ?? 0) > 0) InfoRow('Recurring deductions', formatMoney(-((p['recurringDeductions'] as num))) ),
            if (((p['noticeShortfallDeduction'] as num?) ?? 0) > 0) InfoRow('Notice not served (${days(p['noticeShortfallDays'])} days)', formatMoney(-((p['noticeShortfallDeduction'] as num)))),
            if (((p['loanBalance'] as num?) ?? 0) > 0) InfoRow('Loans (balance ${formatMoney(p['loanBalance'] as num)})', formatMoney(-((p['loanRecovery'] as num?) ?? 0))),
            InfoRow('Net to pay', formatMoney((p['netPay'] as num?) ?? 0), bold: true),
          ]),
        if (p != null && ((p['unrecoveredLoan'] as num?) ?? 0) > 0)
          Padding(padding: const EdgeInsets.all(ErpSpace.s2), child: Text('${formatMoney(p['unrecoveredLoan'] as num)} of loans cannot be recovered from this settlement.', style: TextStyle(color: Colors.red.shade700))),
        if (p != null) for (final n in (p['notes'] as List<dynamic>? ?? const [])) Padding(padding: const EdgeInsets.all(ErpSpace.s2), child: Text('$n')),
        if (_error != null) Padding(padding: const EdgeInsets.all(ErpSpace.s2), child: Text(_error!, style: TextStyle(color: Colors.red.shade700))),
        const SizedBox(height: ErpSpace.s3),
        if (!_ended) ...[
          OutlinedButton(onPressed: _busy || _termination == null ? null : _loadPreview, child: const Text('Preview settlement')),
          const SizedBox(height: ErpSpace.s2),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red.shade700),
            onPressed: _busy || _termination == null ? null : () => _confirmEnd(context),
            child: const Text('End employment'),
          ),
        ] else if (p != null && p['settlementRunId'] != null)
          OutlinedButton(onPressed: () => Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => PayrollDetailScreen(id: '${p['settlementRunId']}'))), child: const Text('Open the settlement payroll'))
        else
          FilledButton(onPressed: _busy ? null : _settle, child: const Text('Prepare final settlement')),
      ]),
    );
  }

  Future<void> _confirmEnd(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text('End employment of ${widget.name}?'),
        content: const Text('The employee is marked as having left and future leave is cancelled.'),
        actions: [TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Keep')), FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('End employment'))],
      ),
    );
    if (ok == true) await _end();
  }
}
