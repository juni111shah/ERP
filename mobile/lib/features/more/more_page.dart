import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/me.dart';
import '../../kit/module_grid.dart';
import '../../kit/registry.dart';
import '../../screens/all_screens.dart';
import '../../theme/app_theme.dart';
import '../assistant/assistant_api.dart';
import '../assistant/assistant_page.dart';
import '../common/paged_list_view.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';

MobileScreen? screenForMenuId(String menuId) {
  for (final s in allScreens) {
    if (s.menuIds.contains(menuId)) return s;
  }
  return null;
}

/// The server's menu for this user and workspace as a module grid, with the entries the app can open. Entries the app
/// does not have yet are listed (so nothing is hidden), dimmed and marked as web only.
class MorePage extends ConsumerWidget {
  const MorePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final menu = ref.watch(menuProvider);
    return menu.when(
      loading: () => const ErpSkeleton.list(),
      error: (e, _) => ErrorView(message: e.toString(), onRetry: () => ref.invalidate(menuProvider)),
      data: (groups) {
        final items = [for (final g in groups) ...g.items];
        final available = items.where((i) => screenForMenuId(i.id) != null).length;
        return RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(assistantAvailableProvider);
            return ref.refresh(menuProvider.future);
          },
          child: ListView(
            padding: EdgeInsets.only(bottom: MediaQuery.paddingOf(context).bottom + 24),
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, 0),
                child: Text('$available of ${items.length} areas are on the phone. The rest are marked "web".',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpTheme.of(context).muted)),
              ),
              if (ref.watch(assistantAvailableProvider).asData?.value == true) const AssistantEntryCard(),
              ModuleGrid(groups: [
                for (final g in groups)
                  ModuleGroup(title: g.label ?? 'General', tiles: [for (final item in g.items) _tile(context, item)]),
              ]),
            ],
          ),
        );
      },
    );
  }

  ModuleTile _tile(BuildContext context, MenuItem item) {
    final screen = screenForMenuId(item.id);
    return ModuleTile(
      label: item.label,
      icon: screen?.icon ?? Icons.language,
      note: screen == null ? 'web' : null,
      onTap: screen == null ? null : () => Navigator.of(context).push(MaterialPageRoute(builder: screen.builder)),
    );
  }
}
