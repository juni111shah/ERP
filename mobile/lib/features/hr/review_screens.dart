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

const reviewsPath = '$hrPath/reviews';
const cyclesPath = '$hrPath/review-cycles';

const reviewStatuses = [Option('draft', 'Draft'), Option('submitted', 'Submitted'), Option('acknowledged', 'Acknowledged')];

FormSpec reviewForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit review' : 'New review',
    initial: {
      'reviewerName': existing?['reviewerName'] ?? (editing ? null : ref.read(meOrEmptyProvider).fullName),
      if (editing) ...{
        'employeeId': existing['employeeId'],
        '__label_employeeId': existing['employeeName'],
        'cycleId': existing['cycleId'],
        '__label_cycleId': existing['cycleName'],
        'overallRating': existing['overallRating'],
        'goals': existing['goals'],
        'comments': existing['comments'],
        'competencies': [
          for (final c in (existing['competencies'] as List<dynamic>? ?? const [])) {'name': (c as Json)['name'], 'score': c['score'], 'notes': c['notes']},
        ],
      },
    },
    fields: [
      Field.lookup('employeeId', 'Employee', path: employeesPathForLookup, labelKey: 'fullName', subtitleKey: 'number', required: true, visibleWhen: (_) => !editing),
      Field.lookup('cycleId', 'Review cycle', path: cyclesPath, required: true, query: const {'filter': 'status:eq:open'}, visibleWhen: (_) => !editing),
      Field.text('reviewerName', 'Reviewer', maxLength: 150),
      Field.integer('overallRating', 'Overall rating (1-5)', min: 1, max: 5),
      Field.multiline('goals', 'Goals', maxLength: 4000),
      Field.multiline('comments', 'Comments', maxLength: 4000),
      Field.lines('competencies', 'Competencies', minLines: 0, addLabel: 'Add competency', fields: [
        Field.text('name', 'Competency', required: true, maxLength: 100),
        Field.integer('score', 'Score (1-5)', required: true, min: 1, max: 5),
        Field.text('notes', 'Notes', maxLength: 1000),
      ]),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {
        ...body,
        if (editing) 'employeeId': existing['employeeId'],
        if (editing) 'cycleId': existing['cycleId'],
        'competencies': body['competencies'] ?? const [],
      };
      return editing ? api.put('$reviewsPath/${existing['id']}', body: full) : api.post(reviewsPath, body: full);
    },
  );
}

const employeesPathForLookup = '$hrPath/employees';

Widget reviewTile(BuildContext context, Json r) {
  final status = (r['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ReviewDetailScreen(id: r['id'].toString()))),
    title: NameText(employeeNames, r['employeeId'], style: const TextStyle(fontSize: 16)),
    subtitle: NameText(cycleNames, r['cycleId']),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      if (r['overallRating'] != null) Text('${r['overallRating']} / 5', style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: 4),
      hrStatusChip(status, humanize(status)),
    ]),
  );
}

class ReviewsScreen extends StatelessWidget {
  const ReviewsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Performance reviews',
        path: reviewsPath,
        emptyText: 'No reviews yet.',
        itemBuilder: reviewTile,
        filters: reviewStatuses,
        create: (ref) => reviewForm(ref),
        createPermission: 'hr.reviews.manage',
      );
}

class ReviewDetailScreen extends StatelessWidget {
  const ReviewDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$reviewsPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => 'Review: ${d['employeeName']}',
          canEdit: (d) => d['status'] == 'draft' && ref.read(meOrEmptyProvider).can('hr.reviews.manage'),
          edit: (d) => reviewForm(ref, existing: d),
          body: (context, d) => reviewBody(context, d),
          actions: [
            ActionSpec(
              label: 'Submit',
              icon: Icons.send_outlined,
              permission: 'hr.reviews.manage',
              visible: (d) => d['status'] == 'draft',
              confirm: 'Submit this review? It can no longer be edited.',
              successMessage: 'Submitted',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/submit'),
            ),
            ActionSpec(
              label: 'Acknowledge',
              icon: Icons.thumb_up_alt_outlined,
              permission: 'hr.reviews.manage',
              visible: (d) => d['status'] == 'submitted',
              confirm: 'Mark this review as acknowledged by the employee?',
              successMessage: 'Acknowledged',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/acknowledge'),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'hr.reviews.manage',
              destructive: true,
              visible: (d) => d['status'] == 'draft',
              confirm: 'Delete this draft review?',
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

Widget reviewBody(BuildContext context, Json d) {
  final status = '${d['status']}';
  final comps = (d['competencies'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: '${d['employeeName']}', subtitle: '${d['cycleName']}'),
    Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: 12), child: hrStatusChip(status, humanize(status)))),
    InfoCard(rows: [
      InfoRow('Reviewer', text(d['reviewerName'])),
      InfoRow('Overall rating', d['overallRating'] == null ? null : '${d['overallRating']} / 5'),
      InfoRow('Goals', text(d['goals'])),
      InfoRow('Comments', text(d['comments'])),
    ]),
    if (comps.isNotEmpty) Text('Competencies', style: Theme.of(context).textTheme.titleMedium),
    for (final c in comps)
      ListTile(contentPadding: EdgeInsets.zero, title: Text('${c['name']}'), subtitle: (c['notes'] ?? '').toString().isEmpty ? null : Text('${c['notes']}'), trailing: Text('${c['score']} / 5')),
  ]);
}

FormSpec cycleForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New review cycle',
    initial: {if (editing) ...{'name': existing['name'], 'periodStart': existing['periodStart'], 'periodEnd': existing['periodEnd'], 'status': existing['status']}},
    fields: [
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.date('periodStart', 'Period start', required: true, initial: 'today'),
      Field.date('periodEnd', 'Period end', required: true, initial: 'today', validator: (v, all) {
        final s = all['periodStart']?.toString() ?? '';
        return v != null && s.isNotEmpty && '$v'.compareTo(s) < 0 ? 'The end is before the start' : null;
      }),
      Field.select('status', 'Status', options: const [Option('open', 'Open'), Option('closed', 'Closed')], required: true, initial: 'open'),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$cyclesPath/${existing['id']}', body: body) : api.post(cyclesPath, body: body);
    },
  );
}

class ReviewCyclesScreen extends ConsumerWidget {
  const ReviewCyclesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => ListScreen(
        title: 'Review cycles',
        path: cyclesPath,
        emptyText: 'No review cycles yet.',
        itemBuilder: (context, c) => ListTile(
          onTap: ref.read(meOrEmptyProvider).can('hr.reviews.manage') ? () => openForm<Object?>(context, cycleForm(ref, existing: c)) : null,
          title: Text('${c['name']}'),
          subtitle: Text('${formatDate(c['periodStart'])} - ${formatDate(c['periodEnd'])}'),
          trailing: hrStatusChip('${c['status']}', humanize('${c['status']}')),
        ),
        create: (ref) => cycleForm(ref),
        createPermission: 'hr.reviews.manage',
      );
}
