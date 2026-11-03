import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/palette_registry.dart';
import '../../core/session.dart';
import '../../kit/bottom_nav.dart';
import '../../kit/brand_mark.dart';
import '../../kit/command_palette.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../dashboard/dashboard_page.dart';
import '../receivables/invoice_screens.dart';
import '../receivables/party_screens.dart';
import '../receivables/receipt_screens.dart';
import '../more/more_page.dart';
import '../onboarding/onboarding_tour.dart';
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
  void initState() {
    super.initState();
    // First arrival in the signed-in shell on this device: show the coach tour once (flag in the existing store).
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) OnboardingTour.maybeShowAfterSignIn(context, ref.read(storeProvider));
    });
  }

  /// Opens the command palette; tab entries switch this shell's tab, everything else navigates.
  Future<void> _openPalette() => showCommandPalette(
        context,
        entries: buildPaletteEntries(),
        onSelect: (entry) => openPaletteEntry(context, ref, entry, selectTab: (i) => setState(() => _index = i)),
      );

  @override
  Widget build(BuildContext context) {
    final workspace = ref.watch(sessionProvider).workspace;
    final t = ErpTheme.of(context);
    return CallbackShortcuts(
      bindings: {
        const SingleActivator(LogicalKeyboardKey.keyK, control: true): _openPalette,
        const SingleActivator(LogicalKeyboardKey.keyK, meta: true): _openPalette,
      },
      child: Focus(
        autofocus: true,
        child: Scaffold(
          extendBody: true, // the floating notched bar sits over the content
          appBar: AppBar(
            titleSpacing: ErpSpace.s4,
            toolbarHeight: 60,
            title: Row(
              children: [
                const BrandMark(size: 36),
                const SizedBox(width: ErpSpace.s25),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(_titles[_index], style: Theme.of(context).textTheme.titleMedium?.copyWith(fontSize: ErpType.titleLarge)),
                      if (workspace != null)
                        Text(workspace.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: ErpType.caption, color: t.muted, fontWeight: FontWeight.w500)),
                    ],
                  ),
                ),
              ],
            ),
            actions: [
              IconButton(
                key: const ValueKey('header-search'),
                tooltip: 'Search',
                constraints: const BoxConstraints.tightFor(width: 48, height: 48),
                icon: Icon(Icons.search_rounded, size: 24, color: t.text2),
                onPressed: _openPalette,
              ),
              const HomeHeaderActions(),
            ],
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
            onLongPress: (i) {
              if (i == 0) _openPalette();
            },
            items: const [
              ErpNavItem(icon: Icons.dashboard_outlined, activeIcon: Icons.dashboard, label: 'Overview'),
              ErpNavItem(icon: Icons.receipt_long_outlined, activeIcon: Icons.receipt_long, label: 'Invoices'),
              ErpNavItem(icon: Icons.payments_outlined, activeIcon: Icons.payments, label: 'Receipts'),
              ErpNavItem(icon: Icons.people_outline, activeIcon: Icons.people, label: 'Customers'),
              ErpNavItem(icon: Icons.apps_outlined, activeIcon: Icons.apps, label: 'More'),
            ],
          ),
        ),
      ),
    );
  }
}
