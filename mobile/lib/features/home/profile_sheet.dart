import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/me.dart';
import '../../core/session.dart';
import '../../core/theme_mode.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../notifications/notifications.dart';
import '../settings/settings_page.dart';

/// Up to two initials from a name, else the first letter of the email.
String initialsFor(String name, String email) {
  final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isNotEmpty) {
    final a = parts.first.characters.first;
    final b = parts.length > 1 ? parts.last.characters.first : '';
    return (a + b).toUpperCase();
  }
  final e = email.trim();
  return e.isEmpty ? '?' : e.characters.first.toUpperCase();
}

/// The header's trailing actions: the notifications bell with its count, then the account avatar (dot when unread).
class HomeHeaderActions extends ConsumerWidget {
  const HomeHeaderActions({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = ErpTheme.of(context);
    final me = ref.watch(meOrEmptyProvider);
    final unread = ref.watch(notificationsProvider).asData?.value.unread ?? 0;
    return Row(mainAxisSize: MainAxisSize.min, children: [
      IconButton(
        key: const ValueKey('header-bell'),
        tooltip: 'Notifications',
        constraints: const BoxConstraints.tightFor(width: 48, height: 48),
        icon: Badge(
          key: const ValueKey('bell-badge'),
          isLabelVisible: unread > 0,
          backgroundColor: ErpColors.danger,
          textColor: Colors.white,
          label: Text(unread > 99 ? '99+' : '$unread', textDirection: TextDirection.ltr),
          child: Icon(Icons.notifications_none_rounded, size: 24, color: t.text2),
        ),
        onPressed: () => showNotificationsSheet(context),
      ),
      Padding(
        padding: const EdgeInsetsDirectional.only(end: ErpSpace.s2),
        child: Tooltip(
          message: 'Account',
          child: Semantics(
            button: true,
            label: 'Account',
            excludeSemantics: true,
            child: InkResponse(
              key: const ValueKey('header-avatar'),
              radius: 24,
              onTap: () => showProfileSheet(context, ref),
              child: SizedBox(
                width: 48,
                height: 48,
                child: Center(
                  child: Badge(
                    key: const ValueKey('avatar-dot'),
                    isLabelVisible: unread > 0,
                    smallSize: 10,
                    backgroundColor: ErpColors.danger,
                    offset: const Offset(-1, 1),
                    child: UserAvatar(name: me.fullName, email: me.email, size: 36),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    ]);
  }
}

class UserAvatar extends StatelessWidget {
  const UserAvatar({super.key, required this.name, required this.email, this.size = 36});

  final String name;
  final String email;
  final double size;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(gradient: t.heroGradient, shape: BoxShape.circle, border: Border.all(color: t.surface, width: 2)),
      child: Text(initialsFor(name, email), style: TextStyle(color: Colors.white, fontSize: size * 0.38, fontWeight: FontWeight.w700)),
    );
  }
}

/// Opens the account sheet. [ref] belongs to the header, so actions that outlive the sheet (sign out, switching) still have a live ref.
Future<void> showProfileSheet(BuildContext context, WidgetRef ref) =>
    showErpSheet<void>(context, initial: 0.62, builder: (ctx, controller) => ProfileSheet(controller: controller, hostContext: context, hostRef: ref));

class ProfileSheet extends ConsumerWidget {
  const ProfileSheet({super.key, this.controller, required this.hostContext, required this.hostRef});

  final ScrollController? controller;
  final BuildContext hostContext;
  final WidgetRef hostRef;

  void _then(BuildContext context, void Function() action) {
    Navigator.of(context).pop();
    action();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = ErpTheme.of(context);
    final me = ref.watch(meOrEmptyProvider);
    final session = ref.watch(sessionProvider);
    final workspace = session.workspace;
    final workspaces = session.info?.workspaces ?? const [];
    final unread = ref.watch(notificationsProvider).asData?.value.unread ?? 0;
    final mode = ref.watch(themeModeProvider);
    final name = me.fullName.isNotEmpty ? me.fullName : (me.email.isNotEmpty ? me.email : 'Signed in');

    Widget row({Key? key, required IconData icon, required String label, Widget? trailing, Color? color, VoidCallback? onTap}) => InkWell(
          key: key,
          onTap: onTap,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 52),
            child: Padding(
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4),
              child: Row(children: [
                Icon(icon, size: 22, color: color ?? t.text2),
                const SizedBox(width: ErpSpace.s4),
                Expanded(child: Text(label, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: color ?? t.text))),
                ?trailing,
                if (onTap != null && trailing == null) Icon(Icons.chevron_right_rounded, size: 20, color: t.muted),
              ]),
            ),
          ),
        );

    return ListView(
      controller: controller,
      padding: const EdgeInsets.only(bottom: 24),
      children: [
        // Who is signed in, and where.
        Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s4),
          child: Row(children: [
            UserAvatar(name: me.fullName, email: me.email, size: 52),
            const SizedBox(width: ErpSpace.s3),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: t.text)),
                if (me.email.isNotEmpty && me.email != name)
                  Text(me.email, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: t.text2)),
                if (workspace != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Container(
                      padding: const EdgeInsetsDirectional.symmetric(horizontal: 10, vertical: 3),
                      decoration: BoxDecoration(color: t.brandSubtle, borderRadius: BorderRadius.circular(ErpRadius.pill)),
                      child: Row(mainAxisSize: MainAxisSize.min, children: [
                        Icon(Icons.business_outlined, size: 14, color: t.brand),
                        const SizedBox(width: 4),
                        Flexible(child: Text(workspace.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: t.brand))),
                      ]),
                    ),
                  ),
              ]),
            ),
          ]),
        ),
        Divider(height: 1, color: t.borderSubtle),
        row(
          key: const ValueKey('profile-notifications'),
          icon: Icons.notifications_none_rounded,
          label: 'Notifications',
          trailing: Row(mainAxisSize: MainAxisSize.min, children: [
            if (unread > 0) Badge.count(count: unread, backgroundColor: ErpColors.danger, textColor: Colors.white),
            Icon(Icons.chevron_right_rounded, size: 20, color: t.muted),
          ]),
          onTap: () => _then(context, () => showNotificationsSheet(hostContext)),
        ),
        // Appearance.
        Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, ErpSpace.s2),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Icon(Icons.palette_outlined, size: 22, color: t.text2),
              const SizedBox(width: ErpSpace.s4),
              Text('Appearance', style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500, color: t.text)),
            ]),
            const SizedBox(height: ErpSpace.s2),
            SizedBox(
              width: double.infinity,
              child: SegmentedButton<ThemeMode>(
                key: const ValueKey('appearance'),
                showSelectedIcon: false,
                style: const ButtonStyle(minimumSize: WidgetStatePropertyAll(Size(0, 48))),
                segments: const [
                  ButtonSegment(value: ThemeMode.light, icon: Icon(Icons.light_mode_outlined, size: 18), label: Text('Light')),
                  ButtonSegment(value: ThemeMode.dark, icon: Icon(Icons.dark_mode_outlined, size: 18), label: Text('Dark')),
                  ButtonSegment(value: ThemeMode.system, icon: Icon(Icons.brightness_auto_outlined, size: 18), label: Text('System')),
                ],
                selected: {mode},
                onSelectionChanged: (s) => ref.read(themeModeProvider.notifier).set(s.first),
              ),
            ),
          ]),
        ),
        if (workspaces.length > 1)
          row(
            key: const ValueKey('profile-switch'),
            icon: Icons.swap_horiz_rounded,
            label: 'Switch workspace',
            onTap: () => _then(context, () => _showWorkspaces(hostContext, hostRef)),
          ),
        row(
          key: const ValueKey('profile-settings'),
          icon: Icons.settings_outlined,
          label: 'Settings',
          onTap: () => _then(context, () => hostContext.push('/settings')),
        ),
        row(
          key: const ValueKey('profile-about'),
          icon: Icons.info_outline_rounded,
          label: 'About',
          onTap: () => _then(context, () => showAboutDialog(context: hostContext, applicationName: 'ERP')),
        ),
        Divider(height: 1, color: t.borderSubtle),
        row(
          key: const ValueKey('profile-signout'),
          icon: Icons.logout_rounded,
          label: 'Sign out',
          color: Theme.of(context).colorScheme.error,
          trailing: const SizedBox.shrink(),
          onTap: () => _then(context, () => confirmSignOut(hostContext, hostRef)),
        ),
      ],
    );
  }
}

Future<void> _showWorkspaces(BuildContext context, WidgetRef ref) => showErpSheet<void>(
      context,
      initial: 0.5,
      builder: (ctx, controller) => Consumer(builder: (ctx, sheetRef, _) {
        final t = ErpTheme.of(ctx);
        final session = sheetRef.watch(sessionProvider);
        final current = session.workspace;
        final workspaces = session.info?.workspaces ?? const [];
        return ListView(controller: controller, children: [
          Padding(
            padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s2),
            child: Text('Switch workspace', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: t.text)),
          ),
          for (final w in workspaces)
            ListTile(
              minTileHeight: 56,
              leading: Icon(w.id == current?.id ? Icons.radio_button_checked : Icons.radio_button_unchecked, color: w.id == current?.id ? t.brand : t.muted),
              title: Text(w.name),
              subtitle: w.code.isEmpty ? null : Text(w.code),
              onTap: w.id == current?.id
                  ? null
                  : () {
                      Navigator.of(ctx).pop();
                      switchWorkspaceWithFeedback(context, ref, w.id);
                    },
            ),
        ]);
      }),
    );
