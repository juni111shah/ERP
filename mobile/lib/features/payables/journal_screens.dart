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
import 'common.dart';

const journalsPath = '$finPath/journals';

const journalStatuses = [Option('draft', 'Draft'), Option('posted', 'Posted'), Option('reversed', 'Reversed')];

num _sum(Object? lines, String key) => [for (final l in (lines as List<dynamic>? ?? const [])) toNum((l as Map)[key]) ?? 0].fold<num>(0, (a, b) => a + b);

/// Debits and credits of the lines must match, otherwise the server refuses the journal.
String? balanceCheck(Object? lines, Values all) {
  final d = _sum(lines, 'debit');
  final c = _sum(lines, 'credit');
  if ((d - c).abs() > 0.004) return 'Debits (${formatMoney(d)}) and credits (${formatMoney(c)}) must be equal';
  return null;
}

/// Create (existing == null) or edit a draft manual journal.
FormSpec journalForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  final initial = <String, dynamic>{
    'legalEntityId': existing?['legalEntityId'] ?? ref.read(meOrEmptyProvider).defaultEntity?.id,
    if (editing) ...{
      'date': existing['date'],
      'memo': existing['memo'],
      'reference': existing['reference'],
      'lines': [
        for (final l in (existing['lines'] as List<dynamic>))
          {
            'accountId': (l as Json)['accountId'],
            '__label_accountId': '${l['accountCode']} ${l['accountName']}',
            'debit': (l['debit'] as num) > 0 ? l['debit'] : null,
            'credit': (l['credit'] as num) > 0 ? l['credit'] : null,
            'description': l['description'],
          },
      ],
    },
  };
  String? oneSide(Object? v, Values line) {
    final d = toNum(line['debit']) ?? 0;
    final c = toNum(line['credit']) ?? 0;
    return (d > 0) == (c > 0) ? 'Enter either a debit or a credit' : null;
  }

  final fields = <Field>[
    entityField(ref),
    Field.date('date', 'Date', required: true, initial: 'today', validator: plausibleDate),
    Field.text('memo', 'Memo', maxLength: 500),
    Field.text('reference', 'Reference', maxLength: 100),
    Field.lines('lines', 'Lines', minLines: 2, fields: [
      accountField('accountId', 'Account'),
      Field.money('debit', 'Debit'),
      Field.money('credit', 'Credit', validator: oneSide),
      Field.text('description', 'Description', maxLength: 400),
    ]),
  ];
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New journal',
    initial: initial,
    fields: fields,
    summary: (context, raw) {
      final d = _sum(raw['lines'], 'debit');
      final c = _sum(raw['lines'], 'credit');
      return Column(children: [
        InfoRow('Total debit', formatMoney(d)),
        InfoRow('Total credit', formatMoney(c)),
        InfoRow('Difference', formatMoney(d - c), bold: true),
      ]);
    },
    submit: (ref, body, raw) async {
      final unbalanced = balanceCheck(raw['lines'], raw);
      if (unbalanced != null) throw ApiException(422, unbalanced);
      final lines = [
        for (final l in (body['lines'] as List<dynamic>)) {...(l as Map<String, dynamic>), 'debit': l['debit'] ?? 0, 'credit': l['credit'] ?? 0},
      ];
      final b = {...body, 'lines': lines};
      final api = ref.read(apiProvider);
      return editing ? api.put('$journalsPath/${existing['id']}', body: b) : api.post(journalsPath, body: b);
    },
  );
}

Widget journalTile(BuildContext context, Json j) {
  final status = (j['status'] ?? '').toString();
  return ListTile(
    onTap: () => pushPage(context, JournalDetailScreen(id: j['id'].toString())),
    title: Text('${j['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${(j['memo'] ?? '').toString().isEmpty ? humanize(j['sourceType']?.toString()) : j['memo']}\n${formatDate(j['date'])}', maxLines: 2, overflow: TextOverflow.ellipsis),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(j, 'amount'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class JournalsScreen extends StatelessWidget {
  const JournalsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Journals',
        path: journalsPath,
        searchHint: 'Search journals',
        emptyText: 'No journals found.',
        itemBuilder: journalTile,
        filters: journalStatuses,
        create: (ref) => journalForm(ref),
        createPermission: 'finance.journals.write',
      );
}

bool _isManualDraft(Json d) => d['status'] == 'draft' && d['sourceType'] == 'manual';

class JournalDetailScreen extends StatelessWidget {
  const JournalDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$journalsPath/$id',
        titleOf: (d) => d['number'].toString(),
        canEdit: (d) => _isManualDraft(d) && ref.read(meOrEmptyProvider).can('finance.journals.write'),
        edit: (d) => journalForm(ref, existing: d),
        body: journalBody,
        actions: [
          ActionSpec(
            label: 'Post',
            icon: Icons.check_circle_outline,
            permission: 'finance.journals.post',
            visible: (d) => d['status'] == 'draft',
            confirm: 'Post this journal to the ledger?',
            successMessage: 'Journal posted',
            run: (ref, d, _) => ref.read(apiProvider).post('$journalsPath/$id/post'),
          ),
          ActionSpec(
            label: 'Reverse',
            icon: Icons.undo,
            permission: 'finance.journals.reverse',
            destructive: true,
            visible: (d) => d['status'] == 'posted' && d['sourceType'] == 'manual' && d['reversalOfId'] == null,
            reasonLabel: 'Why is this journal being reversed?',
            successMessage: 'Reversal posted',
            run: (ref, d, reason) => ref.read(apiProvider).post('$journalsPath/$id/reverse', body: {'memo': reason}),
          ),
          ActionSpec(
            label: 'Delete',
            icon: Icons.delete_outline,
            permission: 'finance.journals.write',
            destructive: true,
            visible: _isManualDraft,
            confirm: 'Delete this draft journal?',
            successMessage: 'Deleted',
            run: (ref, d, _) async {
              await ref.read(apiProvider).delete('$journalsPath/$id');
              if (context.mounted) Navigator.of(context).pop();
              return null;
            },
          ),
        ],
      );
    });
  }
}

Widget journalBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: (d['memo'] ?? d['number']).toString(), subtitle: '${formatDate(d['date'])} · ${humanize(d['sourceType']?.toString())}', status: d['status']?.toString()),
    InfoCard(rows: [
      InfoRow('Reference', d['reference']?.toString()),
      InfoRow('Amount', money(d, 'amount'), bold: true),
    ]),
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['accountCode']} ${l['accountName']}'),
        subtitle: (l['description'] ?? '').toString().isEmpty ? null : Text(l['description'].toString()),
        trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
          if (((l['debit'] as num?) ?? 0) > 0) Text('Dr ${formatMoney(l['debit'] as num)}'),
          if (((l['credit'] as num?) ?? 0) > 0) Text('Cr ${formatMoney(l['credit'] as num)}'),
        ]),
      ),
  ]);
}
