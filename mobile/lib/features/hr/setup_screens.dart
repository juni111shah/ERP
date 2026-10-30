import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../kit/data_version.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'hr_common.dart';

const _typesPath = '$hrPath/leave-types';
const _holidaysPath = '$hrPath/holidays';

FormSpec leaveTypeForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New leave type',
    initial: {
      if (editing) ...{
        'code': existing['code'],
        'name': existing['name'],
        'isPaid': existing['isPaid'],
        'annualDays': existing['annualDays'],
        'carryForwardCap': existing['carryForwardCap'],
        'requiresApproval': existing['requiresApproval'],
        'isActive': existing['isActive'],
      },
    },
    fields: [
      Field.text('code', 'Code', required: true, maxLength: 20),
      Field.text('name', 'Name', required: true, maxLength: 100),
      Field.toggle('isPaid', 'Paid leave', initial: true),
      Field.decimal('annualDays', 'Days per year', required: true, min: 0, decimals: 2, initial: '0'),
      Field.decimal('carryForwardCap', 'Carry-forward limit (days)', required: true, min: 0, decimals: 2, initial: '0'),
      Field.toggle('requiresApproval', 'Needs approval', initial: true),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_typesPath/${existing['id']}', body: body) : api.post(_typesPath, body: body);
    },
  );
}

Widget leaveTypeTile(BuildContext context, WidgetRef ref, Json t) {
  final active = t['isActive'] != false;
  return ListTile(
    onTap: ref.read(meOrEmptyProvider).can('hr.leave.approve') ? () => openForm<Object?>(context, leaveTypeForm(ref, existing: t)) : null,
    title: Text('${t['name']}'),
    subtitle: Text('${t['code']} · ${t['isPaid'] == true ? 'Paid' : 'Unpaid'} · ${days(t['annualDays'])} days a year'),
    trailing: StatusChip(active ? 'Active' : 'Inactive', color: active ? Colors.green.shade700 : Colors.grey.shade700),
  );
}

class LeaveTypesScreen extends ConsumerWidget {
  const LeaveTypesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => ListScreen(
        title: 'Leave types',
        path: _typesPath,
        emptyText: 'No leave types yet.',
        itemBuilder: (context, t) => leaveTypeTile(context, ref, t),
        create: (ref) => leaveTypeForm(ref),
        createPermission: 'hr.leave.approve',
      );
}

FormSpec holidayForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit holiday' : 'New public holiday',
    initial: {if (editing) ...{'date': existing['date'], 'name': existing['name']}},
    fields: [
      Field.date('date', 'Date', required: true, initial: 'today'),
      Field.text('name', 'Name', required: true, maxLength: 150),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_holidaysPath/${existing['id']}', body: body) : api.post(_holidaysPath, body: body);
    },
  );
}

class HolidaysScreen extends StatelessWidget {
  const HolidaysScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Public holidays',
        path: _holidaysPath,
        emptyText: 'No public holidays.',
        itemBuilder: (context, h) => ListTile(
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => HolidayDetailScreen(holiday: h))),
          title: Text('${h['name']}'),
          subtitle: Text(formatDate(h['date'])),
        ),
        create: (ref) => holidayForm(ref),
        createPermission: 'hr.leave.approve',
      );
}

/// A holiday has no single-record endpoint, so the list row is the record; edit and delete close the screen when done.
class HolidayDetailScreen extends ConsumerWidget {
  const HolidayDetailScreen({super.key, required this.holiday});

  final Json holiday;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canManage = ref.watch(meOrEmptyProvider).can('hr.leave.approve');
    return Scaffold(
      appBar: AppBar(title: Text('${holiday['name']}')),
      body: DetailBody(children: [
        InfoCard(rows: [InfoRow('Date', formatDate(holiday['date'])), InfoRow('Name', '${holiday['name']}')]),
        if (canManage) ...[
          FilledButton.icon(
            onPressed: () async {
              final saved = await openForm<Object?>(context, holidayForm(ref, existing: holiday));
              if (saved != null && context.mounted) Navigator.of(context).pop();
            },
            icon: const Icon(Icons.edit_outlined),
            label: const Text('Edit'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              final nav = Navigator.of(context);
              try {
                await ref.read(apiProvider).delete('$_holidaysPath/${holiday['id']}');
                ref.read(dataVersionProvider.notifier).bump();
                nav.pop();
              } on ApiException catch (e) {
                messenger.showSnackBar(SnackBar(content: Text(e.message)));
              }
            },
            icon: const Icon(Icons.delete_outline),
            label: const Text('Delete'),
          ),
        ],
      ]),
    );
  }
}
