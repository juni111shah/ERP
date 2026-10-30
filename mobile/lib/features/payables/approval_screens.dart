import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../common/paged_list_view.dart' show StatusChip;
import 'api_view.dart';

const approvalsPath = '/api/v1/approvals';

/// The approvals list answers a plain array; tolerate a paged answer too.
List<Json> approvalRows(dynamic data) {
  final list = data is List ? data : (data is Map ? (data['items'] as List<dynamic>? ?? const []) : const []);
  return list.cast<Json>();
}

String documentTypeLabel(Object? t) => switch (t?.toString()) {
      'bill' => 'Vendor bill',
      'payment' => 'Payment',
      'invoice' => 'Invoice',
      final other => humanize(other),
    };

class ApprovalsScreen extends ConsumerStatefulWidget {
  const ApprovalsScreen({super.key});

  @override
  ConsumerState<ApprovalsScreen> createState() => _ApprovalsScreenState();
}

class _ApprovalsScreenState extends ConsumerState<ApprovalsScreen> {
  bool _mine = false;

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Approvals')),
        body: Column(children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Row(children: [
              ChoiceChip(label: const Text('To decide'), selected: !_mine, onSelected: (_) => setState(() => _mine = false)),
              const SizedBox(width: 8),
              ChoiceChip(label: const Text('My requests'), selected: _mine, onSelected: (_) => setState(() => _mine = true)),
            ]),
          ),
          Expanded(
            child: ApiView(
              path: approvalsPath,
              query: {if (_mine) 'scope': 'mine'},
              builder: (context, data) {
                final rows = approvalRows(data);
                if (rows.isEmpty) return ListView(children: const [Padding(padding: EdgeInsets.all(48), child: Center(child: Text('No approvals found.')))]);
                return ListView(children: [for (final r in rows) ApprovalTile(approval: r)]);
              },
            ),
          ),
        ]),
      );
}

class ApprovalTile extends ConsumerWidget {
  const ApprovalTile({super.key, required this.approval});

  final Json approval;

  Future<void> _decide(BuildContext context, WidgetRef ref, bool approve) async {
    final controller = TextEditingController();
    final messenger = ScaffoldMessenger.of(context);
    final comment = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(approve ? 'Approve' : 'Reject'),
        content: TextField(controller: controller, maxLines: 3, decoration: const InputDecoration(border: OutlineInputBorder(), hintText: 'Comment (optional)')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, controller.text.trim()), child: Text(approve ? 'Approve' : 'Reject')),
        ],
      ),
    );
    if (comment == null) return;
    try {
      await ref.read(apiProvider).post('$approvalsPath/${approval['id']}/${approve ? 'approve' : 'reject'}', body: {if (comment.isNotEmpty) 'comment': comment});
      ref.read(dataVersionProvider.notifier).bump();
      messenger.showSnackBar(SnackBar(content: Text(approve ? 'Approved' : 'Rejected')));
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message), duration: const Duration(seconds: 6)));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = (approval['status'] ?? '').toString();
    final pending = status == 'pending';
    final canDecide = pending && ref.watch(meOrEmptyProvider).can('core.approvals.decide');
    final amount = approval['amount'] as num?;
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Expanded(child: Text('${approval['subject']}', style: Theme.of(context).textTheme.titleSmall)),
            StatusChip(humanize(status), color: StatusChip.forStatus(status)),
          ]),
          const SizedBox(height: 4),
          Text('${documentTypeLabel(approval['documentType'])} · requested ${formatDate((approval['requestedAt'] ?? '').toString().split('T').first)}'),
          if (amount != null) Text('${approval['currencyCode'] ?? ''} ${formatMoney(amount)}'.trim(), style: const TextStyle(fontWeight: FontWeight.w600)),
          if ((approval['justification'] ?? '').toString().isNotEmpty) Text('${approval['justification']}'),
          if ((approval['decisionComment'] ?? '').toString().isNotEmpty) Text('Comment: ${approval['decisionComment']}'),
          if (canDecide)
            Align(
              alignment: Alignment.centerRight,
              child: Wrap(spacing: 8, children: [
                OutlinedButton(
                  style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                  onPressed: () => _decide(context, ref, false),
                  child: const Text('Reject'),
                ),
                FilledButton(onPressed: () => _decide(context, ref, true), child: const Text('Approve')),
              ]),
            ),
        ]),
      ),
    );
  }
}
