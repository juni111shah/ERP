import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import '../core/me.dart';
import '../core/session.dart';
import '../features/common/paged_list_view.dart';
import 'data_version.dart';
import 'form_page.dart';

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

/// Loads one record and shows it with an app-bar edit button and a bar of actions. [body] draws the content.
class DetailPage extends ConsumerStatefulWidget {
  const DetailPage({super.key, required this.path, required this.titleOf, required this.body, this.actions = const [], this.edit, this.canEdit});

  final String path;
  final String Function(Json data) titleOf;
  final Widget Function(BuildContext context, Json data) body;
  final List<ActionSpec> actions;

  /// Builds the edit form for the loaded record; popped value is ignored, the page reloads.
  final FormSpec Function(Json data)? edit;
  final bool Function(Json data)? canEdit;

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
          ? const Center(child: CircularProgressIndicator())
          : _error != null && data == null
              ? ErrorView(message: _error!, onRetry: _load)
              : RefreshIndicator(onRefresh: _load, child: widget.body(context, data!)),
      bottomNavigationBar: actions.isEmpty
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 6, 12, 10),
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  alignment: WrapAlignment.end,
                  children: [
                    for (final a in actions)
                      a.destructive
                          ? OutlinedButton.icon(
                              style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                              onPressed: _acting ? null : () => _runAction(a, data!),
                              icon: Icon(a.icon),
                              label: Text(a.label))
                          : FilledButton.icon(onPressed: _acting ? null : () => _runAction(a, data!), icon: Icon(a.icon), label: Text(a.label)),
                  ],
                ),
              ),
            ),
    );
  }
}
