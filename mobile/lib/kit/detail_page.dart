import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import '../core/me.dart';
import '../core/session.dart';
import '../features/common/paged_list_view.dart';
import 'data_version.dart';
import 'form_page.dart';
import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import 'skeleton.dart';

typedef Json = Map<String, dynamic>;

/// A button on a detail screen that calls the API (issue, void, post, approve...).
class ActionSpec {
  const ActionSpec({
    required this.label,
    required this.run,
    this.icon = Icons.play_arrow,
    this.visible,
    this.confirm,
    this.reasonLabel,
    this.successMessage,
    this.destructive = false,
    this.form,
    this.permission,
  });

  final String label;
  final IconData icon;

  /// Hide the action for records it does not apply to (for example only drafts can be issued).
  final bool Function(Json data)? visible;

  /// Ask "are you sure" with this text first.
  final String? confirm;

  /// Ask for a reason (required, 3+ characters) and pass it to [run].
  final String? reasonLabel;
  final String? successMessage;
  final bool destructive;

  /// When set, the action opens this form instead of running directly (for example "record a payment"). [run] is then unused.
  final FormSpec Function(Json data)? form;

  /// Permission key (for example `finance.invoices.issue`) the user needs to see this action.
  final String? permission;

  final Future<Object?> Function(WidgetRef ref, Json data, String? reason) run;
}

/// How many actions the bar shows before the rest move under "More".
const _maxBarActions = 2;

/// Loads one record and shows it with an app-bar edit button and a bar of actions. [body] draws the content.
class DetailPage extends ConsumerStatefulWidget {
  const DetailPage({super.key, required this.path, required this.titleOf, required this.body, this.actions = const [], this.edit, this.canEdit, this.headerActions});

  final String path;
  final String Function(Json data) titleOf;
  final Widget Function(BuildContext context, Json data) body;
  final List<ActionSpec> actions;

  /// Builds the edit form for the loaded record; popped value is ignored, the page reloads.
  final FormSpec Function(Json data)? edit;
  final bool Function(Json data)? canEdit;

  /// Extra app-bar buttons for the loaded record (for example Print / Share PDF), shown before Edit.
  final List<Widget> Function(BuildContext context, Json data)? headerActions;

  @override
  ConsumerState<DetailPage> createState() => _DetailPageState();
}

class _DetailPageState extends ConsumerState<DetailPage> {
  Json? _data;
  String? _error;
  bool _loading = true;
  bool _acting = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = _data == null;
      _error = null;
    });
    try {
      final data = await ref.read(apiProvider).get(widget.path);
      if (mounted) setState(() => _data = data as Json);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<String?> _askReason(String label) {
    final controller = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (ctx) {
        String? error;
        return StatefulBuilder(
          builder: (ctx, setLocal) => AlertDialog(
            title: Text(label),
            content: TextField(controller: controller, autofocus: true, maxLines: 3, decoration: InputDecoration(border: const OutlineInputBorder(), errorText: error, hintText: 'At least 3 characters')),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
              FilledButton(
                onPressed: () {
                  if (controller.text.trim().length < 3) {
                    setLocal(() => error = 'Enter a reason (3+ characters)');
                    return;
                  }
                  Navigator.pop(ctx, controller.text.trim());
                },
                child: const Text('Confirm'),
              ),
            ],
          ),
        );
      },
    );
  }

  Future<bool> _confirm(String text, {bool destructive = false}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text(text),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(
            style: destructive ? FilledButton.styleFrom(backgroundColor: Theme.of(ctx).colorScheme.error) : null,
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Continue'),
          ),
        ],
      ),
    );
    return ok == true;
  }

  Future<void> _runAction(ActionSpec a, Json data) async {
    final messenger = ScaffoldMessenger.of(context);
    if (a.form != null) {
      final saved = await openForm<Object?>(context, a.form!(data));
      if (saved != null || mounted) await _load();
      return;
    }
    if (a.confirm != null && !await _confirm(a.confirm!, destructive: a.destructive)) return;
    String? reason;
    if (a.reasonLabel != null) {
      reason = await _askReason(a.reasonLabel!);
      if (reason == null) return;
    }
    setState(() => _acting = true);
    try {
      await a.run(ref, data, reason);
      ref.read(dataVersionProvider.notifier).bump();
      messenger.showSnackBar(SnackBar(content: Text(a.successMessage ?? '${a.label} done')));
      await _load();
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message), duration: const Duration(seconds: 6)));
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final data = _data;
    final me = ref.watch(meOrEmptyProvider);
    final actions = data == null ? const <ActionSpec>[] : widget.actions.where((a) => me.can(a.permission) && (a.visible?.call(data) ?? true)).toList();
    return Scaffold(
      appBar: AppBar(
        title: Text(data == null ? 'Loading' : widget.titleOf(data)),
        actions: [
          if (data != null && widget.headerActions != null) ...widget.headerActions!(context, data),
          if (data != null && widget.edit != null && (widget.canEdit?.call(data) ?? true))
            IconButton(
              tooltip: 'Edit',
              icon: const Icon(Icons.edit_outlined),
              onPressed: () async {
                await openForm<Object?>(context, widget.edit!(data));
                if (mounted) await _load();
              },
            ),
        ],
      ),
      body: _loading
          ? const ErpSkeleton.detail()
          : _error != null && data == null
              ? ErrorView(message: _error!, onRetry: _load)
              : RefreshIndicator(onRefresh: _load, child: widget.body(context, data!)),
      bottomNavigationBar: actions.isEmpty ? null : _actionBar(context, actions, data!),
    );
  }

  /// The actions that do not fit in the bar, with the destructive ones apart at the bottom.
  Future<void> _showMore(List<ActionSpec> rest, Json data) async {
    final plain = rest.where((a) => !a.destructive).toList();
    final danger = rest.where((a) => a.destructive).toList();
    final chosen = await showModalBottomSheet<ActionSpec>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (ctx) {
        final error = Theme.of(ctx).colorScheme.error;
        return SafeArea(
          child: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              for (final a in plain) ListTile(leading: Icon(a.icon), title: Text(a.label), onTap: () => Navigator.pop(ctx, a)),
              if (plain.isNotEmpty && danger.isNotEmpty) const Divider(height: ErpSpace.s3),
              for (final a in danger) ListTile(leading: Icon(a.icon, color: error), title: Text(a.label, style: TextStyle(color: error)), onTap: () => Navigator.pop(ctx, a)),
            ]),
          ),
        );
      },
    );
    if (chosen != null && mounted) await _runAction(chosen, data);
  }

  /// Sticky action bar: destructive actions as outlined error buttons, the rest as filled brand buttons (the first one
  /// solid, others tonal). One or two actions share a row (primary wider, as in the mockup). With more, so the bar never
  /// covers the record, the first [_maxBarActions] that are not destructive stay and the rest are under More: order a
  /// screen's actions with the everyday ones first.
  Widget _actionBar(BuildContext context, List<ActionSpec> actions, Json data) {
    final t = ErpTheme.of(context);
    final error = Theme.of(context).colorScheme.error;
    final ordered = [...actions.where((a) => a.destructive), ...actions.where((a) => !a.destructive)];
    final primary = actions.where((a) => !a.destructive).firstOrNull;
    final collapsed = actions.length > _maxBarActions;
    final inBar = actions.where((a) => !a.destructive).take(_maxBarActions).toList();
    Widget button(ActionSpec a) {
      final onPressed = _acting ? null : () => _runAction(a, data);
      // A long label shrinks a little rather than being cut off.
      final label = FittedBox(fit: BoxFit.scaleDown, child: Text(a.label, maxLines: 1));
      final icon = Icon(a.icon, size: 18);
      if (a.destructive) {
        return OutlinedButton.icon(
          style: OutlinedButton.styleFrom(foregroundColor: error, side: BorderSide(color: error.withValues(alpha: 0.45))),
          onPressed: onPressed,
          icon: icon,
          label: label,
        );
      }
      return identical(a, primary)
          ? FilledButton.icon(onPressed: onPressed, icon: icon, label: label)
          : FilledButton.tonalIcon(
              style: FilledButton.styleFrom(backgroundColor: t.brandSubtle, foregroundColor: t.brandHover),
              onPressed: onPressed,
              icon: icon,
              label: label);
    }

    // Three buttons share the collapsed bar, so they use less side padding to keep their labels whole on a phone.
    final base = Theme.of(context);
    const compact = ButtonStyle(padding: WidgetStatePropertyAll(EdgeInsets.symmetric(horizontal: ErpSpace.s3)));

    return DecoratedBox(
      decoration: BoxDecoration(color: t.surface, border: Border(top: BorderSide(color: t.borderSubtle))),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s25, ErpSpace.s4, ErpSpace.s25),
          child: collapsed
              ? Theme(
                  data: base.copyWith(
                    filledButtonTheme: FilledButtonThemeData(style: compact.merge(base.filledButtonTheme.style)),
                    outlinedButtonTheme: OutlinedButtonThemeData(style: compact.merge(base.outlinedButtonTheme.style)),
                  ),
                  child: Row(mainAxisAlignment: MainAxisAlignment.end, children: [
                    for (final a in inBar) ...[
                      Expanded(child: button(a)),
                      const SizedBox(width: ErpSpace.s2),
                    ],
                    OutlinedButton.icon(
                      key: const ValueKey('detail-more'),
                      onPressed: _acting ? null : () => _showMore(actions.where((a) => !inBar.contains(a)).toList(), data),
                      icon: const Icon(Icons.more_horiz, size: 18),
                      label: const Text('More'),
                    ),
                  ]),
                )
              : Row(children: [
                  for (var i = 0; i < ordered.length; i++) ...[
                    if (i > 0) const SizedBox(width: ErpSpace.s2),
                    Expanded(flex: identical(ordered[i], primary) && ordered.length > 1 ? 16 : 10, child: button(ordered[i])),
                  ],
                ]),
        ),
      ),
    );
  }
}
