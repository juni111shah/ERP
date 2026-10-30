import 'package:flutter/material.dart';

import '../../core/format.dart';
import '../common/paged_list_view.dart';

// Receipts and customers: read-only for now. The receivables area replaces these with full screens (create, void, allocate...).

String _money(Map<String, dynamic> m, String key) => '${m['currencyCode'] ?? ''} ${formatMoney((m[key] as num?) ?? 0)}'.trim();

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
