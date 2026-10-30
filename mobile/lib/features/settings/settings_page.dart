import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/session.dart';

class SettingsPage extends ConsumerWidget {
  const SettingsPage({super.key});

  Future<void> _switch(BuildContext context, WidgetRef ref, String id) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(sessionProvider.notifier).switchWorkspace(id);
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Sign out?'),
        content: const Text('You will need to sign in again.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Sign out')),
        ],
      ),
    );
    if (ok == true) await ref.read(sessionProvider.notifier).logout();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionProvider);
    final current = session.workspace;
    final workspaces = session.info?.workspaces ?? const [];
    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        children: [
          ListTile(
            leading: const Icon(Icons.business_outlined),
            title: Text(current?.name ?? 'No workspace'),
            subtitle: Text(current == null ? '' : [current.code, current.industry].where((s) => s.isNotEmpty).join(' · ')),
          ),
          if (workspaces.length > 1) ...[
            const Padding(padding: EdgeInsets.fromLTRB(16, 8, 16, 4), child: Text('Switch workspace')),
            for (final w in workspaces)
              ListTile(
                leading: Icon(w.id == current?.id ? Icons.radio_button_checked : Icons.radio_button_unchecked),
                title: Text(w.name),
                subtitle: Text(w.code),
                onTap: w.id == current?.id ? null : () => _switch(context, ref, w.id),
              ),
          ],
          const Divider(),
          ListTile(leading: const Icon(Icons.dns_outlined), title: const Text('Server'), subtitle: Text(ref.watch(serverUrlProvider))),
          ListTile(
            leading: Icon(Icons.logout, color: Theme.of(context).colorScheme.error),
            title: const Text('Sign out'),
            onTap: () => _logout(context, ref),
          ),
        ],
      ),
    );
  }
}
