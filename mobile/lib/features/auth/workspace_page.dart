import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/session.dart';
import '../../kit/brand_mark.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';

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
              padding: const EdgeInsets.all(ErpSpace.s4),
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: ErpSpace.s1, bottom: ErpSpace.s4),
                  child: Align(alignment: AlignmentDirectional.centerStart, child: BrandLockup(markSize: 36)),
                ),
                Padding(
                  padding: const EdgeInsets.only(bottom: ErpSpace.s3),
                  child: Text('Your account belongs to more than one workspace.', style: TextStyle(fontSize: ErpType.bodySmall, color: ErpTheme.of(context).muted)),
                ),
                if (_error != null) Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
                for (final w in pending.workspaces)
                  Card(
                    margin: const EdgeInsets.only(bottom: ErpSpace.s2),
                    child: ListTile(
                      contentPadding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s35, ErpSpace.s15, ErpSpace.s25, ErpSpace.s15),
                      leading: Container(
                        width: 40,
                        height: 40,
                        decoration: BoxDecoration(color: ErpTheme.of(context).brandSubtle, borderRadius: BorderRadius.circular(ErpRadius.input)),
                        child: Icon(Icons.business_outlined, color: ErpTheme.of(context).brand),
                      ),
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
