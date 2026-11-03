import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/me.dart';
import '../../core/models.dart';
import '../../core/session.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../dashboard/dashboard_page.dart';
import '../dashboard/overview_logic.dart';
import '../../kit/motion.dart';
import '../../kit/skeleton.dart';
import '../../kit/state_view.dart';

/// One row in the notifications list: a server notification, or a dashboard alert when the server list is unavailable.
class AppNotification {
  const AppNotification({required this.id, required this.title, this.body, this.link, this.severity = 'info', this.createdAt, this.unread = false});

  final String id;
  final String title;
  final String? body;
  final String? link;
  final String severity;
  final DateTime? createdAt;
  final bool unread;

  /// `NotificationDto` from `GET /api/v1/notifications`.
  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: (json['id'] ?? '').toString(),
        title: (json['title'] ?? '').toString(),
        body: json['body'] as String?,
        link: json['link'] as String?,
        severity: (json['severity'] ?? 'info').toString().toLowerCase(),
        createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString())?.toLocal(),
        unread: json['readAt'] == null,
      );

  factory AppNotification.fromAlert(DashboardAlert a, int i) => AppNotification(
        id: 'alert-$i',
        title: a.message,
        body: a.category.isEmpty ? null : a.category,
        link: a.link,
        severity: a.severity,
        unread: true,
      );
}

class NotificationFeed {
  const NotificationFeed({required this.items, required this.unread, this.fromAlerts = false});

  final List<AppNotification> items;
  final int unread;

  /// True when the server list failed and these are the dashboard's alerts instead.
  final bool fromAlerts;

  static const empty = NotificationFeed(items: [], unread: 0);

  /// `NotificationList(int Unread, IReadOnlyList<NotificationDto> Items)`.
  factory NotificationFeed.fromJson(Map<String, dynamic> json) {
    final items = [for (final e in (json['items'] as List<dynamic>? ?? const [])) AppNotification.fromJson(e as Map<String, dynamic>)];
    final unread = json['unread'];
    return NotificationFeed(items: items, unread: unread is num ? unread.toInt() : items.where((n) => n.unread).length);
  }

  factory NotificationFeed.fromAlerts(List<DashboardAlert> alerts) =>
      NotificationFeed(items: [for (var i = 0; i < alerts.length; i++) AppNotification.fromAlert(alerts[i], i)], unread: alerts.length, fromAlerts: true);
}

/// The signed-in user's notifications. Falls back to the Overview alerts if the endpoint fails. Nothing is marked read.
final notificationsProvider = FutureProvider.autoDispose<NotificationFeed>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  try {
    final data = await ref.watch(apiProvider).get('/api/v1/notifications');
    return NotificationFeed.fromJson(data as Map<String, dynamic>);
  } catch (_) {
    try {
      final dashboard = await ref.watch(dashboardProvider.future);
      return NotificationFeed.fromAlerts(dashboard.alerts);
    } catch (_) {
      return NotificationFeed.empty;
    }
  }
});

/// Rounded, draggable bottom sheet shell shared by the account and notifications sheets.
Future<T?> showErpSheet<T>(BuildContext context, {required Widget Function(BuildContext context, ScrollController controller) builder, double initial = 0.6}) {
  final t = ErpTheme.of(context);
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    backgroundColor: t.surface,
    shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
    // Quick start, soft landing; none at all under reduced motion.
    sheetAnimationStyle: ErpMotion.reduced(context)
        ? AnimationStyle.noAnimation
        : const AnimationStyle(duration: ErpMotion.slow, reverseDuration: ErpMotion.medium, curve: ErpMotion.spring, reverseCurve: Curves.easeInCubic),
    builder: (ctx) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: initial,
      minChildSize: 0.3,
      maxChildSize: 0.95,
      builder: builder,
    ),
  );
}

Future<void> showNotificationsSheet(BuildContext context) =>
    showErpSheet<void>(context, builder: (ctx, controller) => NotificationsSheet(controller: controller));

class NotificationsSheet extends ConsumerWidget {
  const NotificationsSheet({super.key, this.controller});

  final ScrollController? controller;

  static (IconData, ErpTone) _style(String severity) => switch (severity) {
        'critical' || 'error' || 'danger' => (Icons.error_outline, ErpTone.error),
        'warning' => (Icons.warning_amber_rounded, ErpTone.warning),
        'success' => (Icons.check_circle_outline, ErpTone.success),
        _ => (Icons.notifications_none_rounded, ErpTone.info),
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = ErpTheme.of(context);
    final b = Theme.of(context).brightness;
    final feed = ref.watch(notificationsProvider);
    final menu = ref.watch(menuProvider).asData?.value ?? const <MenuGroup>[];
    final data = feed.asData?.value;
    final header = Padding(
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s2),
      child: Row(children: [
        Expanded(child: Text('Notifications', style: ErpType.titleStyle.copyWith(fontSize: ErpType.titleLarge, color: t.text))),
        if (data != null && data.unread > 0)
          Text('${data.unread} unread', style: TextStyle(fontSize: ErpType.bodySmall, color: t.muted, fontWeight: FontWeight.w500)),
      ]),
    );
    final List<Widget> body;
    if (data == null && feed.hasError) {
      body = [
        ErpStateView.error(
          key: const ValueKey('notifications-error'),
          compact: true,
          onRetry: () => ref.invalidate(notificationsProvider),
        ),
      ];
    } else if (data == null) {
      body = [const ErpSkeleton.notifications()];
    } else if (data.items.isEmpty) {
      body = [
        const ErpStateView.empty(
          key: ValueKey('notifications-empty'),
          icon: Icons.notifications_none_rounded,
          title: "You're all caught up",
          message: 'No notifications right now.',
        ),
      ];
    } else {
      body = [
        if (data.fromAlerts)
          Padding(
            padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s2),
            child: Text('Showing Overview alerts', style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
          ),
        for (var i = 0; i < data.items.length; i++)
          Builder(builder: (context) {
            final n = data.items[i];
            final style = _style(n.severity);
            final screen = screenForLink(n.link, menu: menu);
            return InkWell(
              key: ValueKey('notification-$i'),
              onTap: screen == null
                  ? null
                  : () {
                      final nav = Navigator.of(context);
                      nav.pop();
                      nav.push(MaterialPageRoute(builder: screen.builder));
                    },
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 56),
                child: Padding(
                  padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s25, ErpSpace.s3, ErpSpace.s25),
                  child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(color: style.$2.bgFor(b), shape: BoxShape.circle, border: Border.all(color: style.$2.borderFor(b))),
                      child: Icon(style.$1, size: 18, color: style.$2.fgFor(b)),
                    ),
                    const SizedBox(width: ErpSpace.s3),
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(n.title, style: TextStyle(fontSize: ErpType.body, fontWeight: n.unread ? FontWeight.w600 : FontWeight.w500, color: t.text)),
                        if (n.body != null && n.body!.isNotEmpty)
                          Padding(padding: const EdgeInsets.only(top: ErpSpace.s05), child: Text(n.body!, maxLines: 3, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: ErpType.caption, color: t.text2))),
                        if (n.createdAt != null)
                          Padding(
                            padding: const EdgeInsets.only(top: ErpSpace.s1),
                            child: Text(DateFormat('d MMM, HH:mm').format(n.createdAt!), style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                          ),
                      ]),
                    ),
                    if (n.unread && !data.fromAlerts)
                      Padding(
                        padding: const EdgeInsetsDirectional.only(start: ErpSpace.s2, top: ErpSpace.s15),
                        child: Container(width: 8, height: 8, decoration: BoxDecoration(color: t.brand, shape: BoxShape.circle)),
                      ),
                    if (screen != null) Icon(Icons.chevron_right_rounded, size: 20, color: t.muted),
                  ]),
                ),
              ),
            );
          }),
      ];
    }
    return ListView(controller: controller, padding: const EdgeInsets.only(bottom: ErpSpace.s6), children: [header, ...body]);
  }
}
