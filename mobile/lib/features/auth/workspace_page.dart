import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/session.dart';

/// Shown after sign-in when the account belongs to more than one workspace.
class WorkspacePage extends ConsumerStatefulWidget {
  const WorkspacePage({super.key});

  @override
  ConsumerState<WorkspacePage> createState() => _WorkspacePageState();
}

class _WorkspacePageState extends ConsumerState<WorkspacePage> {
  String? _busyId;
  String? _error;

  Future<void> _choose(PendingWorkspaceSelection pending, String id) async {
    setState(() {
      _busyId = id;
      _error = null;
    });
    try {
      await ref.read(sessionProvider.notifier).selectWorkspace(pending.selectionToken, id);
      ref.read(pendingWorkspaceProvider.notifier).set(null);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(pendingWorkspaceProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Choose a workspace'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => ref.read(pendingWorkspaceProvider.notifier).set(null)),
      ),
      body: pending == null
          ? const SizedBox.shrink()
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (_error != null) Padding(padding: const EdgeInsets.only(bottom: 12), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
                for (final w in pending.workspaces)
                  Card(
                    child: ListTile(
                      leading: const CircleAvatar(child: Icon(Icons.business_outlined)),
                      title: Text(w.name),
                      subtitle: Text([w.code, w.industry].where((s) => s.isNotEmpty).join(' · ')),
                      trailing: _busyId == w.id ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.chevron_right),
                      onTap: _busyId == null ? () => _choose(pending, w.id) : null,
                    ),
                  ),
              ],
            ),
    );
  }
}
