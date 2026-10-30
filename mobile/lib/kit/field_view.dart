import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import '../core/format.dart';
import '../core/models.dart';
import '../core/session.dart';
import 'field.dart';

/// Renders one [Field] against the map [values] it reads and writes. [errors] holds messages keyed like [FormEngine.validate]
/// (line fields use "lines[0].price"); [path] is the prefix of this field's container ("" at the top, "lines[0]." inside a line).
class FieldView extends ConsumerStatefulWidget {
  const FieldView({super.key, required this.field, required this.values, required this.onChanged, this.errors = const {}, this.path = ''});

  final Field field;
  final Values values;
  final VoidCallback onChanged;
  final Map<String, String> errors;
  final String path;

  @override
  ConsumerState<FieldView> createState() => _FieldViewState();
}

class _FieldViewState extends ConsumerState<FieldView> {
  TextEditingController? _controller;

  Field get f => widget.field;
  String? get _error => widget.errors['${widget.path}${f.key}'];

  @override
  void initState() {
    super.initState();
    if (const {FieldType.text, FieldType.multiline, FieldType.integer, FieldType.decimal, FieldType.money}.contains(f.type)) {
      _controller = TextEditingController(text: widget.values[f.key]?.toString() ?? '');
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  /// Keeps a text box in step when something else (a lookup copy, a reset) changed the value.
  void _sync() {
    final c = _controller;
    if (c == null) return;
    final v = widget.values[f.key]?.toString() ?? '';
    if (c.text != v) c.text = v;
  }

  @override
  Widget build(BuildContext context) {
    if (f.visibleWhen != null && !f.visibleWhen!(widget.values)) return const SizedBox.shrink();
    _sync();
    return Padding(padding: const EdgeInsets.only(bottom: 12), child: _input(context));
  }

  void _set(Object? value) {
    widget.values[f.key] = value;
    widget.onChanged();
  }

  Widget _input(BuildContext context) {
    final label = f.required ? '${f.label} *' : f.label;
    switch (f.type) {
      case FieldType.text || FieldType.multiline || FieldType.integer || FieldType.decimal || FieldType.money:
        return TextField(
          controller: _controller,
          readOnly: f.readOnly,
          keyboardType: f.keyboard ?? (f.isNumeric ? const TextInputType.numberWithOptions(decimal: true, signed: true) : (f.type == FieldType.multiline ? TextInputType.multiline : TextInputType.text)),
          inputFormatters: f.formatters,
          maxLength: f.maxLength,
          maxLines: f.type == FieldType.multiline ? 4 : 1,
          textCapitalization: f.type == FieldType.text ? TextCapitalization.sentences : TextCapitalization.none,
          decoration: InputDecoration(labelText: label, hintText: f.hint, errorText: _error, border: const OutlineInputBorder(), counterText: ''),
          onChanged: (v) => _set(v),
        );
      case FieldType.date:
        final current = widget.values[f.key]?.toString();
        return InkWell(
          onTap: () async {
            final initial = DateTime.tryParse(current ?? '') ?? DateTime.now();
            final picked = await showDatePicker(context: context, initialDate: initial, firstDate: DateTime(2000), lastDate: DateTime.now().add(const Duration(days: 365 * 2)));
            if (picked != null) _set(todayIsoOf(picked));
          },
          child: InputDecorator(
            decoration: InputDecoration(labelText: label, errorText: _error, border: const OutlineInputBorder(), suffixIcon: const Icon(Icons.calendar_today_outlined)),
            child: Text(current == null || current.isEmpty ? '' : formatDate(current)),
          ),
        );
      case FieldType.select:
        final current = widget.values[f.key]?.toString();
        return DropdownButtonFormField<String>(
          initialValue: f.options.any((o) => o.value == current) ? current : null,
          isExpanded: true,
          decoration: InputDecoration(labelText: label, errorText: _error, border: const OutlineInputBorder()),
          items: [for (final o in f.options) DropdownMenuItem(value: o.value, child: Text(o.label, overflow: TextOverflow.ellipsis))],
          onChanged: f.readOnly ? null : (v) => _set(v),
        );
      case FieldType.lookup:
        final shown = widget.values['__label_${f.key}']?.toString() ?? '';
        return InkWell(
          onTap: f.readOnly ? null : () => _pick(context),
          child: InputDecorator(
            decoration: InputDecoration(
              labelText: label,
              errorText: _error,
              border: const OutlineInputBorder(),
              suffixIcon: widget.values[f.key] != null && !f.required
                  ? IconButton(icon: const Icon(Icons.clear), onPressed: () {
                      widget.values.remove(f.key);
                      widget.values.remove('__label_${f.key}');
                      widget.onChanged();
                    })
                  : const Icon(Icons.arrow_drop_down),
            ),
            child: Text(shown.isEmpty ? 'Select' : shown, style: shown.isEmpty ? TextStyle(color: Theme.of(context).hintColor) : null),
          ),
        );
      case FieldType.toggle:
        return SwitchListTile(contentPadding: EdgeInsets.zero, title: Text(f.label), value: widget.values[f.key] == true, onChanged: f.readOnly ? null : (v) => _set(v));
      case FieldType.lines:
        return LinesEditor(field: f, values: widget.values, onChanged: widget.onChanged, errors: widget.errors, path: widget.path);
    }
  }

  Future<void> _pick(BuildContext context) async {
    final picked = await showLookupSheet(context, ref, path: f.lookupPath!, title: f.label, labelKey: f.lookupLabelKey, subtitleKey: f.lookupSubtitleKey, query: f.lookupQuery);
    if (picked == null) return;
    widget.values[f.key] = picked['id']?.toString();
    widget.values['__label_${f.key}'] = picked[f.lookupLabelKey]?.toString() ?? '';
    f.copy.forEach((target, source) {
      final v = picked[source];
      if (v != null) widget.values[target] = v;
    });
    widget.onChanged();
  }
}

String todayIsoOf(DateTime d) => '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// Search a list endpoint and return the chosen record (the whole JSON map) or null.
Future<Map<String, dynamic>?> showLookupSheet(BuildContext context, WidgetRef ref,
    {required String path, required String title, String labelKey = 'name', String? subtitleKey, Map<String, dynamic> query = const {}}) {
  return showModalBottomSheet<Map<String, dynamic>>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (ctx) => FractionallySizedBox(
      heightFactor: 0.85,
      child: _LookupSheet(path: path, title: title, labelKey: labelKey, subtitleKey: subtitleKey, query: query),
    ),
  );
}

class _LookupSheet extends ConsumerStatefulWidget {
  const _LookupSheet({required this.path, required this.title, required this.labelKey, required this.subtitleKey, required this.query});

  final String path;
  final String title;
  final String labelKey;
  final String? subtitleKey;
  final Map<String, dynamic> query;

  @override
  ConsumerState<_LookupSheet> createState() => _LookupSheetState();
}

class _LookupSheetState extends ConsumerState<_LookupSheet> {
  List<Map<String, dynamic>> _items = [];
  bool _loading = true;
  String? _error;
  Timer? _debounce;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    _load('');
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _load(String search) async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await ref.read(apiProvider).get(widget.path, query: {'page': 1, 'pageSize': 30, ...widget.query, if (search.isNotEmpty) 'search': search});
      if (!mounted || generation != _generation) return;
      final items = data is List ? [for (final i in data) i as Map<String, dynamic>] : Paged.fromJson(data as Map<String, dynamic>).items;
      setState(() => _items = items);
    } on ApiException catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.message);
    } finally {
      if (mounted && generation == _generation) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Align(alignment: Alignment.centerLeft, child: Text(widget.title, style: Theme.of(context).textTheme.titleMedium))),
        Padding(
          padding: const EdgeInsets.all(12),
          child: SearchBar(
            hintText: 'Search',
            leading: const Icon(Icons.search),
            elevation: const WidgetStatePropertyAll(0),
            onChanged: (t) {
              _debounce?.cancel();
              _debounce = Timer(const Duration(milliseconds: 350), () => _load(t.trim()));
            },
          ),
        ),
        Expanded(
          child: _error != null
              ? Center(child: Text(_error!))
              : (_loading && _items.isEmpty)
                  ? const Center(child: CircularProgressIndicator())
                  : _items.isEmpty
                      ? const Center(child: Text('No matches'))
                      : ListView.builder(
                          itemCount: _items.length,
                          itemBuilder: (context, i) {
                            final item = _items[i];
                            final subtitle = widget.subtitleKey == null ? null : item[widget.subtitleKey]?.toString();
                            return ListTile(
                              title: Text(item[widget.labelKey]?.toString() ?? ''),
                              subtitle: subtitle == null || subtitle.isEmpty ? null : Text(subtitle),
                              onTap: () => Navigator.pop(context, item),
                            );
                          },
                        ),
        ),
      ],
    );
  }
}

/// The repeating group of a document (invoice lines...). Each line is a card of fields with a remove button.
class LinesEditor extends StatelessWidget {
  const LinesEditor({super.key, required this.field, required this.values, required this.onChanged, this.errors = const {}, this.path = ''});

  final Field field;
  final Values values;
  final VoidCallback onChanged;
  final Map<String, String> errors;
  final String path;

  List<Values> get _lines => (values[field.key] as List<dynamic>).cast<Values>();

  @override
  Widget build(BuildContext context) {
    final lines = _lines;
    final error = errors['$path${field.key}'];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(children: [
          Expanded(child: Text(field.label, style: Theme.of(context).textTheme.titleMedium)),
          TextButton.icon(
            onPressed: () {
              lines.add(FormEngine.start(field.lineFields));
              onChanged();
            },
            icon: const Icon(Icons.add),
            label: Text(field.addLabel),
          ),
        ]),
        if (error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(error, style: TextStyle(color: Theme.of(context).colorScheme.error))),
        for (var i = 0; i < lines.length; i++)
          Card(
            key: ObjectKey(lines[i]),
            margin: const EdgeInsets.only(bottom: 10),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 4, 0),
              child: Column(
                children: [
                  Row(children: [
                    Expanded(child: Text('${field.label} ${i + 1}', style: Theme.of(context).textTheme.labelLarge)),
                    if (lines.length > field.minLines)
                      IconButton(
                        tooltip: 'Remove',
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () {
                          lines.removeAt(i);
                          onChanged();
                        },
                      ),
                  ]),
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: Column(children: [
                      for (final sub in field.lineFields)
                        FieldView(key: ValueKey('${field.key}$i${sub.key}${lines[i].hashCode}'), field: sub, values: lines[i], onChanged: onChanged, errors: errors, path: '$path${field.key}[$i].'),
                    ]),
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}
