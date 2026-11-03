import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/me.dart';
import '../../kit/registry.dart';
import '../../screens/all_screens.dart';
import '../common/paged_list_view.dart';

MobileScreen? screenForMenuId(String menuId) {
  for (final s in allScreens) {
    if (s.menuIds.contains(menuId)) return s;
  }
  return null;
}

/// The server's menu for this user and workspace, with the entries the app can open. Entries the app does not have yet are listed
/// (so nothing is hidden) and marked as web only.
class MorePage extends ConsumerWidget {
  const MorePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final menu = ref.watch(menuProvider);
    return menu.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => ErrorView(message: e.toString(), onRetry: () => ref.invalidate(menuProvider)),
      data: (groups) {
        final items = [for (final g in groups) ...g.items];
        final available = items.where((i) => screenForMenuId(i.id) != null).length;
        return RefreshIndicator(
          onRefresh: () async => ref.refresh(menuProvider.future),
          child: ListView(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                child: Text('$available of ${items.length} areas are on the phone. The rest are marked "web".', style: Theme.of(context).textTheme.bodySmall),
              ),
              for (final g in groups)
                if (g.items.isNotEmpty) ...[
                  Padding(padding: const EdgeInsets.fromLTRB(16, 16, 16, 4), child: Text(g.label ?? 'General', style: Theme.of(context).textTheme.titleSmall?.copyWith(color: Theme.of(context).colorScheme.primary))),
                  for (final item in g.items) _tile(context, item),
                ],
              const SizedBox(height: 24),
            ],
          ),
        );
      },
    );
  }

  Widget _tile(BuildContext context, MenuItem item) {
    final screen = screenForMenuId(item.id);
    return ListTile(
      leading: Icon(screen?.icon ?? Icons.language, color: screen == null ? Theme.of(context).disabledColor : null),
      title: Text(item.label, style: screen == null ? TextStyle(color: Theme.of(context).disabledColor) : null),
      trailing: screen == null ? const Text('web', style: TextStyle(fontSize: 12)) : const Icon(Icons.chevron_right),
      enabled: screen != null,
      onTap: screen == null ? null : () => Navigator.of(context).push(MaterialPageRoute(builder: screen.builder)),
    );
  }
}
