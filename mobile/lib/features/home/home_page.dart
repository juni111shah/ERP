import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/session.dart';
import '../dashboard/dashboard_page.dart';
import '../documents/documents_pages.dart';
import '../receivables/invoice_screens.dart';
import '../more/more_page.dart';

/// The signed-in shell: bottom navigation over the main areas. The tab contents are rebuilt when the workspace changes.
class HomePage extends ConsumerStatefulWidget {
  const HomePage({super.key});

  @override
  ConsumerState<HomePage> createState() => _HomePageState();
}

class _HomePageState extends ConsumerState<HomePage> {
  int _index = 0;

  static const _titles = ['Overview', 'Invoices', 'Receipts', 'Customers', 'More'];

  @override
  Widget build(BuildContext context) {
    final workspace = ref.watch(sessionProvider).workspace;
    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[_index]),
        actions: [
          if (workspace != null)
            Padding(
              padding: const EdgeInsets.only(right: 4),
              child: Center(child: Text(workspace.name, style: Theme.of(context).textTheme.labelMedium)),
            ),
          IconButton(tooltip: 'Settings', icon: const Icon(Icons.settings_outlined), onPressed: () => context.push('/settings')),
        ],
      ),
      body: KeyedSubtree(
        key: ValueKey(workspace?.id),
        child: IndexedStack(
          index: _index,
          children: const [DashboardPage(), InvoicesScreen(showAppBar: false), ReceiptsPage(), PartiesPage(), MorePage()],
        ),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Overview'),
          NavigationDestination(icon: Icon(Icons.receipt_long_outlined), selectedIcon: Icon(Icons.receipt_long), label: 'Invoices'),
          NavigationDestination(icon: Icon(Icons.payments_outlined), selectedIcon: Icon(Icons.payments), label: 'Receipts'),
          NavigationDestination(icon: Icon(Icons.people_outline), selectedIcon: Icon(Icons.people), label: 'Customers'),
          NavigationDestination(icon: Icon(Icons.apps_outlined), selectedIcon: Icon(Icons.apps), label: 'More'),
        ],
      ),
    );
  }
}
