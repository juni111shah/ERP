import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/me.dart';
import '../../kit/state_view.dart';
import '../../theme/tokens.dart';
import '../settings/settings_page.dart';

/// What a suspended or cancelled workspace shows instead of its screens: why, that the data is kept, and a way to check again or sign
/// out. It appears the moment the platform suspends the workspace (the server pushes the change, see AccessSync) and goes away the
/// moment access is restored.
class WorkspaceBlockedView extends ConsumerWidget {
  const WorkspaceBlockedView({super.key, required this.me});

  final Me me;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cancelled = me.subscriptionStatus == 'cancelled';
    final reason = me.suspendedReason;
    return Center(
      key: const ValueKey('workspace-blocked'),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ErpStateView(
              icon: Icons.lock_outline_rounded,
              tone: ErpTone.warning,
              title: cancelled ? 'This subscription is cancelled' : 'This workspace is suspended',
              message: [
                'Your data is kept and comes back as soon as access is restored.',
                if (reason != null && reason.isNotEmpty) reason,
                'Your administrator can sort this out under Subscription and billing on the web.',
              ].join('\n\n'),
              actionLabel: 'Check again',
              actionIcon: Icons.refresh_rounded,
              onAction: () => ref.invalidate(meProvider),
            ),
            TextButton.icon(
              key: const ValueKey('workspace-blocked-sign-out'),
              onPressed: () => confirmSignOut(context, ref),
              icon: const Icon(Icons.logout_rounded, size: 18),
              label: const Text('Sign out'),
            ),
          ],
        ),
      ),
    );
  }
}

/// The notice over a workspace whose subscription is view-only: everything can be looked at, changes are paused.
class WorkspaceReadOnlyNotice extends StatelessWidget {
  const WorkspaceReadOnlyNotice({super.key, required this.me});

  final Me me;

  @override
  Widget build(BuildContext context) {
    final why = me.subscriptionStatus == 'pastDue'
        ? 'A payment is overdue.'
        : me.subscriptionStatus == 'trialing' || me.subscriptionStatus == 'expired'
            ? 'The trial has ended.'
            : 'The subscription needs attention.';
    final b = Theme.of(context).brightness;
    return Material(
      key: const ValueKey('workspace-read-only'),
      color: ErpTone.warning.bgFor(b),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4, vertical: ErpSpace.s2),
        child: Row(
          children: [
            Icon(Icons.visibility_outlined, size: 18, color: ErpTone.warning.fgFor(b)),
            const SizedBox(width: ErpSpace.s2),
            Expanded(child: Text('$why You can look at everything, but changes are paused.', style: TextStyle(color: ErpTone.warning.fgFor(b)))),
          ],
        ),
      ),
    );
  }
}
