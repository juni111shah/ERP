import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/session.dart';
import '../../kit/bottom_nav.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../dashboard/dashboard_page.dart';
import '../receivables/invoice_screens.dart';
import '../receivables/party_screens.dart';
import '../receivables/receipt_screens.dart';
import '../more/more_page.dart';
import 'profile_sheet.dart';

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
    final t = ErpTheme.of(context);
    final initials = (workspace?.code ?? workspace?.name ?? 'E').trim();
    return Scaffold(
      extendBody: true, // the floating notched bar sits over the content
      appBar: AppBar(
        titleSpacing: ErpSpace.s4,
        toolbarHeight: 60,
        title: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              alignment: Alignment.center,
              decoration: BoxDecoration(gradient: t.heroGradient, borderRadius: BorderRadius.circular(ErpRadius.input)),
              child: Text(initials.isEmpty ? 'E' : initials.substring(0, initials.length < 2 ? initials.length : 2).toUpperCase(),
                  style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w700)),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(_titles[_index], style: Theme.of(context).textTheme.titleMedium?.copyWith(fontSize: 17)),
                  if (workspace != null)
                    Text(workspace.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: t.muted, fontWeight: FontWeight.w500)),
                ],
              ),
            ),
          ],
        ),
        actions: const [HomeHeaderActions()],
      ),
      body: KeyedSubtree(
        key: ValueKey(workspace?.id),
        child: IndexedStack(
          index: _index,
          children: const [DashboardPage(), InvoicesScreen(showAppBar: false), ReceiptsScreen(showAppBar: false), PartiesScreen(title: 'Customers', role: 'Customer', showAppBar: false), MorePage()],
        ),
      ),
      bottomNavigationBar: ErpBottomNav(
        currentIndex: _index,
        onTap: (i) => setState(() => _index = i),
        items: const [
          ErpNavItem(icon: Icons.dashboard_outlined, activeIcon: Icons.dashboard, label: 'Overview'),
          ErpNavItem(icon: Icons.receipt_long_outlined, activeIcon: Icons.receipt_long, label: 'Invoices'),
          ErpNavItem(icon: Icons.payments_outlined, activeIcon: Icons.payments, label: 'Receipts'),
          ErpNavItem(icon: Icons.people_outline, activeIcon: Icons.people, label: 'Customers'),
          ErpNavItem(icon: Icons.apps_outlined, activeIcon: Icons.apps, label: 'More'),
        ],
      ),
    );
  }
}
