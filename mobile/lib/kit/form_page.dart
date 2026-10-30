import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import 'data_version.dart';
import 'field.dart';
import 'field_view.dart';

/// A screen made from a list of [Field]s.
///
/// [submit] receives the cleaned request body (numbers parsed, lookups as ids, empties dropped) and the raw values, and returns whatever
/// the screen should pop with (usually the saved record). A failure is shown on the screen and, where the server names a field, under it.
class FormSpec {
  const FormSpec({
    required this.title,
    required this.fields,
    required this.submit,
    this.initial = const {},
    this.submitLabel = 'Save',
    this.summary,
    this.intro,
  });

  final String title;
  final List<Field> fields;
  final Values initial;
  final String submitLabel;
  final Future<Object?> Function(WidgetRef ref, Values body, Values raw) submit;

  /// Shown under the fields and rebuilt on every change (totals...).
  final Widget Function(BuildContext context, Values raw)? summary;
  final String? intro;
}

/// Opens [spec] as a full screen and completes with what the form popped with (null when cancelled).
Future<T?> openForm<T>(BuildContext context, FormSpec spec) => Navigator.of(context).push<T>(MaterialPageRoute(builder: (_) => FormPage(spec: spec)));

class FormPage extends ConsumerStatefulWidget {
  const FormPage({super.key, required this.spec});

  final FormSpec spec;

  @override
  ConsumerState<FormPage> createState() => _FormPageState();
}

class _FormPageState extends ConsumerState<FormPage> {
  late final Values _values = FormEngine.start(widget.spec.fields, widget.spec.initial);
  Map<String, String> _errors = {};
  String? _banner;
  bool _busy = false;

  Future<void> _submit() async {
    final errors = FormEngine.validate(widget.spec.fields, _values);
    if (errors.isNotEmpty) {
      setState(() {
        _errors = errors;
        _banner = 'Please fix the highlighted fields.';
      });
      return;
    }
    setState(() {
      _busy = true;
      _errors = {};
      _banner = null;
    });
    try {
      final body = FormEngine.collect(widget.spec.fields, _values);
      final result = await widget.spec.submit(ref, body, _values);
      ref.read(dataVersionProvider.notifier).bump();
      if (mounted) Navigator.pop(context, result);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _banner = e.message;
        _errors = _mapServerErrors(e.fieldErrors, widget.spec.fields);
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// The server names fields like "invoiceDate" or "Input.InvoiceDate" or "lines[0].price"; match them to our keys.
  static Map<String, String> _mapServerErrors(Map<String, List<String>> server, List<Field> fields) {
    final byLower = {for (final f in fields) f.key.toLowerCase(): f.key};
    final result = <String, String>{};
    server.forEach((rawKey, messages) {
      var key = rawKey;
      final dot = key.toLowerCase().startsWith('input.') ? key.substring(6) : key;
      key = dot;
      final lineMatch = RegExp(r'^(\w+)\[(\d+)\]\.(\w+)$').firstMatch(key);
      if (lineMatch != null) {
        final group = byLower[lineMatch.group(1)!.toLowerCase()];
        if (group != null) result['$group[${lineMatch.group(2)}].${_lineKey(fields, group, lineMatch.group(3)!)}'] = messages.join(' ');
        return;
      }
      final own = byLower[key.toLowerCase()];
      if (own != null) result[own] = messages.join(' ');
    });
    return result;
  }

  static String _lineKey(List<Field> fields, String group, String sub) {
    final f = fields.firstWhere((x) => x.key == group);
    return f.lineFields.where((l) => l.key.toLowerCase() == sub.toLowerCase()).map((l) => l.key).firstOrNull ?? sub;
  }

  @override
  Widget build(BuildContext context) {
    final spec = widget.spec;
    return Scaffold(
      appBar: AppBar(title: Text(spec.title)),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(16),
                keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                children: [
                  if (_banner != null)
                    Container(
                      margin: const EdgeInsets.only(bottom: 12),
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(color: Theme.of(context).colorScheme.errorContainer, borderRadius: BorderRadius.circular(8)),
                      child: Text(_banner!, style: TextStyle(color: Theme.of(context).colorScheme.onErrorContainer)),
                    ),
                  if (spec.intro != null) Padding(padding: const EdgeInsets.only(bottom: 12), child: Text(spec.intro!)),
                  for (final f in spec.fields) FieldView(key: ValueKey(f.key), field: f, values: _values, errors: _errors, onChanged: () => setState(() {})),
                  if (spec.summary != null) spec.summary!(context, _values),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _busy ? null : _submit,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    child: _busy ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2)) : Text(spec.submitLabel),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
