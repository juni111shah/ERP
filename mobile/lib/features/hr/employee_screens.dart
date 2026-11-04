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
import 'people_screens.dart';
import 'settlement_screen.dart';
import 'package:erp_mobile/theme/tokens.dart';

const employeesPath = '$hrPath/employees';

const contractTypes = [Option('permanent', 'Permanent'), Option('fixedTerm', 'Fixed term'), Option('probation', 'Probation'), Option('partTime', 'Part time'), Option('contractor', 'Contractor')];

const employeeStatuses = [Option('active', 'Active'), Option('onLeave', 'On leave'), Option('terminated', 'Terminated')];

FormSpec employeeForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final canPay = me.can('hr.salary.read');
  final entityId = existing?['legalEntityId'] ?? me.defaultEntity?.id;
  return FormSpec(
    title: editing ? 'Edit ${existing['fullName']}' : 'New employee',
    initial: {
      'legalEntityId': entityId,
      if (editing) ...{'__label_departmentId': existing['department'], '__label_positionId': existing['designation'], '__label_managerEmployeeId': existing['managerName']},
      if (editing)
        for (final k in ['departmentId', 'positionId', 'managerEmployeeId', 'contractType', 'probationEndDate', 'contractEndDate', 'noticePeriodDays', 'dateOfBirth', 'gender', 'maritalStatus', 'address', 'emergencyContactName', 'emergencyContactPhone', 'bankName', 'fullName', 'designation', 'department', 'joinDate', 'terminationDate', 'email', 'phone', 'nationality', 'status', 'basicSalary', 'housingAllowance', 'transportAllowance', 'otherAllowances', 'recurringDeductions', 'iban', 'labourCardNumber', 'bankRoutingCode'])
          k: existing[k],
    },
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entityId, visibleWhen: (_) => !editing),
      Field.text('fullName', 'Full name', required: true, maxLength: 200),
      Field.text('designation', 'Job title', required: true, maxLength: 150),
      Field.lookup('departmentId', 'Department', path: '$hrPath/departments', labelKey: 'name', subtitleKey: 'code'),
      Field.text('department', 'Department (free text)', maxLength: 100),
      Field.lookup('positionId', 'Position', path: '$hrPath/positions', labelKey: 'title', subtitleKey: 'code'),
      Field.lookup('managerEmployeeId', 'Line manager', path: '$hrPath/employees', labelKey: 'fullName', subtitleKey: 'number'),
      Field.select('contractType', 'Contract type', options: contractTypes),
      Field.date('probationEndDate', 'Probation ends'),
      Field.date('contractEndDate', 'Contract ends'),
      Field.integer('noticePeriodDays', 'Notice period (days)', min: 0, max: 365),
      Field.date('joinDate', 'Join date', required: true, initial: 'today'),
      Field.select('status', 'Status', options: employeeStatuses, required: true, initial: 'active'),
      Field.date('terminationDate', 'Termination date', visibleWhen: (v) => v['status'] == 'terminated', validator: (v, all) {
        if (all['status'] == 'terminated' && (v == null || '$v'.isEmpty)) return 'Enter the termination date';
        final join = all['joinDate']?.toString() ?? '';
        return v != null && join.isNotEmpty && '$v'.compareTo(join) < 0 ? 'Cannot be before the join date' : null;
      }),
      Field.text('email', 'Email', maxLength: 200, keyboard: TextInputType.emailAddress),
      Field.text('phone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
      Field.text('nationality', 'Nationality', maxLength: 100),
      Field.date('dateOfBirth', 'Date of birth'),
      Field.select('gender', 'Gender', options: const [Option('Female', 'Female'), Option('Male', 'Male')]),
      Field.select('maritalStatus', 'Marital status', options: const [Option('Single', 'Single'), Option('Married', 'Married'), Option('Divorced', 'Divorced'), Option('Widowed', 'Widowed')]),
      Field.text('address', 'Address', maxLength: 500),
      Field.text('emergencyContactName', 'Emergency contact', maxLength: 150),
      Field.text('emergencyContactPhone', 'Emergency phone', maxLength: 50, keyboard: TextInputType.phone),
      if (canPay) ...[
        Field.text('bankName', 'Bank name', maxLength: 150),
      Field.money('basicSalary', 'Basic salary', required: true, min: 0.01),
      Field.money('housingAllowance', 'Housing allowance', initial: '0'),
      Field.money('transportAllowance', 'Transport allowance', initial: '0'),
      Field.money('otherAllowances', 'Other allowances', initial: '0'),
      Field.money('recurringDeductions', 'Recurring deductions', initial: '0'),
      Field.text('iban', 'IBAN', maxLength: 50),
      Field.text('labourCardNumber', 'Labour card number', maxLength: 30),
      Field.text('bankRoutingCode', 'Bank routing code', maxLength: 20),
      ],
    ],
    summary: (context, raw) {
      final gross = ['basicSalary', 'housingAllowance', 'transportAllowance', 'otherAllowances'].fold<num>(0, (a, k) => a + (toNum(raw[k]) ?? 0));
      return Padding(padding: const EdgeInsets.only(top: ErpSpace.s1), child: InfoRow('Gross monthly', formatMoney(gross), bold: true));
    },
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {...body, 'legalEntityId': body['legalEntityId'] ?? entityId};
      return editing ? api.put('$employeesPath/${existing['id']}', body: full) : api.post(employeesPath, body: full);
    },
  );
}

Widget employeeTile(BuildContext context, Json e) {
  final status = (e['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => EmployeeDetailScreen(id: e['id'].toString()))),
    leading: CircleAvatar(child: Text(('${e['fullName']}'.isEmpty ? '?' : '${e['fullName']}'[0]).toUpperCase())),
    title: Text('${e['fullName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${e['number']} · ${e['designation']}${(e['department'] ?? '').toString().isEmpty ? '' : ' · ${e['department']}'}', maxLines: 2, overflow: TextOverflow.ellipsis),
    trailing: hrStatusChip(status, humanize(status)),
  );
}

class EmployeesScreen extends StatelessWidget {
  const EmployeesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Employees',
        path: employeesPath,
        searchHint: 'Search employees',
        emptyText: 'No employees found.',
        itemBuilder: employeeTile,
        filters: employeeStatuses,
        create: (ref) => employeeForm(ref),
        createPermission: 'hr.employees.manage',
      );
}

class EmployeeDetailScreen extends StatelessWidget {
  const EmployeeDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$employeesPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['fullName']}',
          canEdit: (_) => ref.read(meOrEmptyProvider).can('hr.employees.manage'),
          edit: (d) => employeeForm(ref, existing: d),
          body: (context, d) => employeeBody(context, d),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'hr.employees.manage',
              destructive: true,
              confirm: 'Delete this employee? This is refused when they appear in payroll or leave.',
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

Widget employeeBody(BuildContext context, Json d) {
  final status = '${d['status']}';
  return DetailBody(children: [
    DetailHeader(title: '${d['fullName']}', subtitle: '${d['number']} · ${d['designation']}'),
    Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: hrStatusChip(status, humanize(status)))),
    _EmployeeActions(d),
    InfoCard(title: 'Details', rows: [
      InfoRow('Department', text(d['department'])),
      InfoRow('Line manager', text(d['managerName'])),
      InfoRow('Contract', d['contractType'] == null ? null : humanize('${d['contractType']}')),
      InfoRow('Notice period', d['noticePeriodDays'] == null ? null : '${d['noticePeriodDays']} days'),
      InfoRow('Emergency contact', text(d['emergencyContactName'])),
      InfoRow('Joined', formatDate(d['joinDate'])),
      InfoRow('Terminated', d['terminationDate'] == null ? null : formatDate(d['terminationDate'])),
      InfoRow('Email', text(d['email'])),
      InfoRow('Phone', text(d['phone'])),
      InfoRow('Nationality', text(d['nationality'])),
      InfoRow('Service', d['serviceYears'] == null ? null : '${days(d['serviceYears'])} years'),
    ]),
    if (d['basicSalary'] != null) InfoCard(title: 'Pay', rows: [
      InfoRow('Basic salary', formatMoney((d['basicSalary'] as num?) ?? 0)),
      InfoRow('Housing', formatMoney((d['housingAllowance'] as num?) ?? 0)),
      InfoRow('Transport', formatMoney((d['transportAllowance'] as num?) ?? 0)),
      InfoRow('Other allowances', formatMoney((d['otherAllowances'] as num?) ?? 0)),
      InfoRow('Recurring deductions', formatMoney((d['recurringDeductions'] as num?) ?? 0)),
      InfoRow('Gross monthly', formatMoney((d['grossMonthly'] as num?) ?? 0), bold: true),
    ]),
    InfoCard(title: 'End of service', rows: [InfoRow('End-of-service benefit so far', d['gratuityAccrued'] == null ? null : formatMoney((d['gratuityAccrued'] as num))), InfoRow('IBAN', text(d['iban'])), InfoRow('Labour card', text(d['labourCardNumber']))]),
  ]);
}

/// Documents, and ending the employment (or the final settlement once the person has left).
class _EmployeeActions extends ConsumerWidget {
  const _EmployeeActions(this.d);

  final Json d;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(meOrEmptyProvider);
    final left = '${d['status']}' == 'terminated';
    return Padding(
      padding: const EdgeInsets.only(bottom: ErpSpace.s3),
      child: Wrap(spacing: ErpSpace.s2, runSpacing: ErpSpace.s2, children: [
        OutlinedButton.icon(
          icon: const Icon(Icons.folder_open),
          label: const Text('Documents'),
          onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => EmployeeDocumentsScreen(employeeId: '${d['id']}', name: '${d['fullName']}'))),
        ),
        if (!left && me.can('hr.employees.manage'))
          OutlinedButton.icon(
            icon: const Icon(Icons.logout),
            label: const Text('End employment'),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => SettlementScreen(employeeId: '${d['id']}', name: '${d['fullName']}'))),
          ),
        if (left && me.can('hr.payroll.manage'))
          OutlinedButton.icon(
            icon: const Icon(Icons.request_quote_outlined),
            label: const Text('Final settlement'),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => SettlementScreen(employeeId: '${d['id']}', name: '${d['fullName']}', terminated: true, terminationDate: d['terminationDate']?.toString()))),
          ),
      ]),
    );
  }
}
