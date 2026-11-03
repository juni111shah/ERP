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
import 'hr_common.dart';
import 'package:erp_mobile/theme/tokens.dart';

const payrollPath = '$hrPath/payroll';

const payrollStatuses = [Option('draft', 'Draft'), Option('pendingApproval', 'Pending approval'), Option('posted', 'Posted'), Option('paid', 'Paid')];

String payrollStatusLabel(String s) => humanize(s);

FormSpec payrollForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  final now = DateTime.now();
  return FormSpec(
    title: 'New payroll run',
    intro: 'Pay and deductions are calculated for every active employee from their salary, attendance and approved leave.',
    submitLabel: 'Generate',
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      Field.integer('year', 'Year', required: true, min: 2000, max: 2100, initial: '${now.year}'),
      Field.integer('month', 'Month (1-12)', required: true, min: 1, max: 12, initial: '${now.month}'),
      Field.date('plannedPayDate', 'Planned pay date'),
      Field.text('notes', 'Notes', maxLength: 500),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post(payrollPath, body: body),
  );
}

/// Pay a posted run from a bank account.
FormSpec payForm(Json run) => FormSpec(
      title: 'Pay ${run['number']}',
      submitLabel: 'Pay',
      fields: [
        Field.lookup('bankAccountId', 'Bank account', path: '/api/v1/finance/accounts', query: const {'filter': 'isBank:eq:true'}, subtitleKey: 'code', required: true),
        Field.date('date', 'Payment date', required: true, initial: 'today'),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$payrollPath/${run['id']}/pay', body: body),
    );

/// Change one employee's pay for a draft run (positive adds, negative deducts).
FormSpec adjustForm(Json run, Json line) => FormSpec(
      title: 'Adjust ${line['employeeName']}',
      initial: {'adjustment': line['adjustment'], 'note': line['adjustmentNote']},
      fields: [
        Field.decimal('adjustment', 'Adjustment (+ adds, - deducts)', required: true, decimals: 2),
        Field.text('note', 'Note', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).put('$payrollPath/${run['id']}/lines/${line['id']}', body: {'adjustment': body['adjustment'], 'note': body['note']}),
    );

Widget payrollTile(BuildContext context, Json r) {
  final status = (r['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => PayrollDetailScreen(id: r['id'].toString()))),
    title: Text('${monthName((r['month'] as num).toInt())} ${r['year']}'),
    subtitle: Text('${r['number']} · ${r['employees']} employees'),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(formatMoney((r['netPay'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      hrStatusChip(status, payrollStatusLabel(status)),
    ]),
  );
}

class PayrollScreen extends StatelessWidget {
  const PayrollScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Payroll',
        path: payrollPath,
        searchHint: 'Search payroll runs',
        emptyText: 'No payroll runs yet.',
        itemBuilder: payrollTile,
        filters: payrollStatuses,
        create: (ref) => payrollForm(ref),
        createPermission: 'hr.payroll.manage',
      );
}

class PayrollDetailScreen extends StatelessWidget {
  const PayrollDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$payrollPath/$id';
        bool open(Json d) => const {'draft', 'pendingApproval'}.contains(d['status']);
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['number']}',
          body: (context, d) => payrollBody(context, ref, d),
          actions: [
            ActionSpec(
              label: 'Recalculate',
              icon: Icons.refresh,
              permission: 'hr.payroll.manage',
              visible: open,
              confirm: 'Recalculate from the current salaries and attendance? Adjustments are kept.',
              successMessage: 'Recalculated',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/recalculate'),
            ),
            ActionSpec(
              label: 'Post',
              icon: Icons.fact_check_outlined,
              permission: 'hr.payroll.post',
              visible: open,
              confirm: 'Post this payroll to the ledger? When approval is required it is sent for approval first.',
              successMessage: 'Done',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/post'),
            ),
            ActionSpec(
              label: 'Pay',
              icon: Icons.payments_outlined,
              permission: 'hr.payroll.pay',
              visible: (d) => d['status'] == 'posted',
              form: payForm,
              run: (ref, d, _) async => null,
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'hr.payroll.manage',
              destructive: true,
              visible: open,
              confirm: 'Delete this payroll run?',
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


Widget payrollBody(BuildContext context, WidgetRef ref, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  final status = '${d['status']}';
  final canAdjust = const {'draft', 'pendingApproval'}.contains(status) && ref.read(meOrEmptyProvider).can('hr.payroll.manage');
  return DetailBody(children: [
    DetailHeader(title: '${monthName((d['month'] as num).toInt())} ${d['year']}', subtitle: '${d['number']} · ${d['employees']} employees'),
    Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: hrStatusChip(status, payrollStatusLabel(status)))),
    if (status == 'pendingApproval') const Padding(padding: EdgeInsets.only(bottom: ErpSpace.s3), child: Text('Waiting for approval. Once approved, tap Post again to post it.')),
    InfoCard(rows: [
      InfoRow('Gross earnings', formatMoney((d['grossEarnings'] as num?) ?? 0)),
      InfoRow('Deductions', formatMoney((d['deductions'] as num?) ?? 0)),
      InfoRow('Net pay', formatMoney((d['netPay'] as num?) ?? 0), bold: true),
      InfoRow('Planned pay date', d['plannedPayDate'] == null ? null : formatDate(d['plannedPayDate'])),
      InfoRow('Paid on', d['paidOn'] == null ? null : formatDate(d['paidOn'])),
      InfoRow('Notes', text(d['notes'])),
    ]),
    Text('Payslips', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      Card(
        child: ExpansionTile(
          title: Text('${l['employeeName']}'),
          subtitle: Text('${l['employeeNumber'] ?? ''}'),
          trailing: Text(formatMoney((l['netPay'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w700)),
          childrenPadding: const EdgeInsets.fromLTRB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s3),
          children: [
            InfoRow('Basic', formatMoney((l['basic'] as num?) ?? 0)),
            InfoRow('Allowances', formatMoney((l['allowances'] as num?) ?? 0)),
            InfoRow('Overtime (${days(l['overtimeHours'])} h)', formatMoney((l['overtime'] as num?) ?? 0)),
            InfoRow('Absence (${days(l['absentDays'])} d)', formatMoney(-((l['absence'] as num?) ?? 0).abs())),
            if (((l['adjustment'] as num?) ?? 0) != 0) InfoRow('Adjustment${(l['adjustmentNote'] ?? '').toString().isEmpty ? '' : ' (${l['adjustmentNote']})'}', formatMoney((l['adjustment'] as num))),
            InfoRow('Deductions', formatMoney((l['deductions'] as num?) ?? 0)),
            InfoRow('Net pay', formatMoney((l['netPay'] as num?) ?? 0), bold: true),
            if (canAdjust)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(onPressed: () => openForm<Object?>(context, adjustForm(d, l)), icon: const Icon(Icons.tune), label: const Text('Adjust')),
              ),
          ],
        ),
      ),
  ]);
}
