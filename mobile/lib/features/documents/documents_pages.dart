import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../common/paged_list_view.dart';

String _money(Map<String, dynamic> m, String key) => '${m['currencyCode'] ?? ''} ${formatMoney((m[key] as num?) ?? 0)}'.trim();

/// Sales invoices and credit notes.
class InvoicesPage extends StatelessWidget {
  const InvoicesPage({super.key});

  @override
  Widget build(BuildContext context) => PagedListView(
        path: '/api/v1/finance/invoices',
        searchHint: 'Search invoices',
        emptyText: 'No invoices found.',
        itemBuilder: (context, i) {
          final status = (i['status'] ?? '').toString();
          final credit = i['isCreditNote'] == true;
          return ListTile(
            onTap: () => context.push('/invoices/${i['id']}'),
            title: Text('${credit ? 'Credit note ' : ''}${i['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text('${i['partyName'] ?? ''}\nDue ${formatDate(i['dueDate'])}'),
            isThreeLine: true,
            trailing: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(_money(i, 'total'), style: const TextStyle(fontWeight: FontWeight.w600)),
                const SizedBox(height: 4),
                StatusChip(humanize(status), color: StatusChip.forStatus(status)),
              ],
            ),
          );
        },
      );
}

/// Customer receipts.
class ReceiptsPage extends StatelessWidget {
  const ReceiptsPage({super.key});

  @override
  Widget build(BuildContext context) => PagedListView(
        path: '/api/v1/finance/receipts',
        searchHint: 'Search receipts',
        emptyText: 'No receipts found.',
        itemBuilder: (context, r) {
          final status = (r['status'] ?? '').toString();
          final unallocated = (r['unallocated'] as num?) ?? 0;
          return ListTile(
            title: Text('${r['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text('${r['partyName'] ?? ''}\n${formatDate(r['date'])} · ${humanize(r['method']?.toString())}'
                '${unallocated > 0 ? ' · ${formatMoney(unallocated)} unallocated' : ''}'),
            isThreeLine: true,
            trailing: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(_money(r, 'amount'), style: const TextStyle(fontWeight: FontWeight.w600)),
                const SizedBox(height: 4),
                StatusChip(humanize(status), color: StatusChip.forStatus(status)),
              ],
            ),
          );
        },
      );
}

/// Customers and vendors.
class PartiesPage extends StatelessWidget {
  const PartiesPage({super.key});

  @override
  Widget build(BuildContext context) => PagedListView(
        path: '/api/v1/parties',
        searchHint: 'Search customers and vendors',
        emptyText: 'No customers or vendors found.',
        itemBuilder: (context, p) {
          final roles = p['roles'] is List ? (p['roles'] as List).map((r) => humanize(r.toString())).join(', ') : '';
          final name = (p['name'] ?? '').toString();
          return ListTile(
            leading: CircleAvatar(child: Text(name.isEmpty ? '?' : name[0].toUpperCase())),
            title: Text(name, maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text([p['code'], roles].where((s) => s != null && s.toString().isNotEmpty).join(' · ')),
          );
        },
      );
}

final invoiceDetailProvider = FutureProvider.autoDispose.family<Map<String, dynamic>, String>((ref, id) async {
  final data = await ref.watch(apiProvider).get('/api/v1/finance/invoices/$id');
  return data as Map<String, dynamic>;
});

class InvoiceDetailPage extends ConsumerWidget {
  const InvoiceDetailPage({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(invoiceDetailProvider(id));
    return Scaffold(
      appBar: AppBar(title: Text(detail.asData?.value['number']?.toString() ?? 'Invoice')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => ErrorView(message: e is ApiException ? e.message : e.toString(), onRetry: () => ref.invalidate(invoiceDetailProvider(id))),
        data: (inv) => _InvoiceBody(inv: inv),
      ),
    );
  }
}

class _InvoiceBody extends StatelessWidget {
  const _InvoiceBody({required this.inv});

  final Map<String, dynamic> inv;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = (inv['status'] ?? '').toString();
    final total = (inv['total'] as num?) ?? 0;
    final paid = (inv['paid'] as num?) ?? 0;
    final lines = (inv['lines'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>();
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(children: [
          Expanded(child: Text(inv['partyName']?.toString() ?? '', style: theme.textTheme.titleLarge)),
          StatusChip(humanize(status), color: StatusChip.forStatus(status)),
        ]),
        const SizedBox(height: 4),
        Text('Invoice date ${formatDate(inv['invoiceDate'])} · Due ${formatDate(inv['dueDate'])}', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(children: [
              _row('Net', _money(inv, 'net')),
              _row('Tax', _money(inv, 'tax')),
              const Divider(),
              _row('Total', _money(inv, 'total'), bold: true),
              _row('Paid', _money(inv, 'paid')),
              _row('Outstanding', '${inv['currencyCode'] ?? ''} ${formatMoney(total - paid)}'.trim(), bold: true),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Text('Lines', style: theme.textTheme.titleMedium),
        const SizedBox(height: 4),
        for (final l in lines)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text('${l['description']}'),
            subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} × ${formatMoney((l['price'] as num?) ?? 0)}'),
            // Quantity x price rather than the stored line net, so a line is right even where the net was never filled in.
            trailing: Text(formatMoney(((l['quantity'] as num?) ?? 0) * ((l['price'] as num?) ?? 0) + ((l['taxAmount'] as num?) ?? 0))),
          ),
        if (inv['notes'] != null && inv['notes'].toString().isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('Notes', style: theme.textTheme.titleMedium),
          Text(inv['notes'].toString()),
        ],
      ],
    );
  }

  Widget _row(String label, String value, {bool bold = false}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [Text(label), Text(value, style: TextStyle(fontWeight: bold ? FontWeight.w700 : FontWeight.w400))],
        ),
      );
}
