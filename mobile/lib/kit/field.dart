import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';

enum FieldType { text, multiline, integer, decimal, money, date, select, lookup, toggle, lines }

class Option {
  const Option(this.value, this.label);

  final String value;
  final String label;
}

typedef Values = Map<String, dynamic>;
typedef VisibleWhen = bool Function(Values values);

/// Describes one input of a form. Build with the named constructors; [FormPage] turns a list of fields into a screen.
///
/// Values live in a `Map<String, dynamic>` keyed by [key]: text as String, numbers as String while typing (use [toNum]),
/// dates as "yyyy-MM-dd", lookups as the chosen record's id, toggles as bool, lines as `List<Map<String, dynamic>>`.
/// [FormEngine.collect] turns them into the request body.
class Field {
  const Field._({
    required this.key,
    required this.label,
    required this.type,
    this.required = false,
    this.hint,
    this.initial,
    this.maxLength,
    this.min,
    this.max,
    this.decimals = 2,
    this.options = const [],
    this.lookupPath,
    this.lookupLabelKey = 'name',
    this.lookupSubtitleKey,
    this.lookupQuery = const {},
    this.initialLabel,
    this.copy = const {},
    this.lineFields = const [],
    this.minLines = 0,
    this.addLabel = 'Add line',
    this.visibleWhen,
    this.keyboard,
    this.readOnly = false,
    this.validator,
  });

  final String key;
  final String label;
  final FieldType type;
  final bool required;
  final String? hint;
  final Object? initial;
  final int? maxLength;
  final num? min;
  final num? max;

  /// Decimal places allowed for [FieldType.decimal] / [FieldType.money].
  final int decimals;
  final List<Option> options;

  /// Lookup: the list endpoint to search, which JSON key is the visible label/subtitle, and fixed extra query parameters.
  final String? lookupPath;
  final String lookupLabelKey;
  final String? lookupSubtitleKey;
  final Map<String, dynamic> lookupQuery;
  final String? initialLabel;

  /// Lookup: after a pick, copy these keys of the chosen record into other fields of the same form/line (`{'price': 'salesPrice'}`).
  final Map<String, String> copy;

  /// Lines: the inputs of one line and how many are needed at least.
  final List<Field> lineFields;
  final int minLines;
  final String addLabel;
  final VisibleWhen? visibleWhen;
  final TextInputType? keyboard;
  final bool readOnly;

  /// Extra check run after the built-in ones; return a message or null.
  final String? Function(Object? value, Values values)? validator;

  factory Field.text(String key, String label,
          {bool required = false, int? maxLength, String? hint, Object? initial, VisibleWhen? visibleWhen, TextInputType? keyboard, bool readOnly = false, String? Function(Object?, Values)? validator}) =>
      Field._(key: key, label: label, type: FieldType.text, required: required, maxLength: maxLength, hint: hint, initial: initial, visibleWhen: visibleWhen, keyboard: keyboard, readOnly: readOnly, validator: validator);

  factory Field.multiline(String key, String label, {bool required = false, int? maxLength, String? hint, Object? initial, VisibleWhen? visibleWhen}) =>
      Field._(key: key, label: label, type: FieldType.multiline, required: required, maxLength: maxLength, hint: hint, initial: initial, visibleWhen: visibleWhen);

  factory Field.integer(String key, String label, {bool required = false, num? min, num? max, Object? initial, VisibleWhen? visibleWhen}) =>
      Field._(key: key, label: label, type: FieldType.integer, required: required, min: min, max: max, initial: initial, visibleWhen: visibleWhen);

  /// A number with [decimals] places (quantities, rates, percentages).
  factory Field.decimal(String key, String label,
          {bool required = false, num? min, num? max, int decimals = 4, Object? initial, VisibleWhen? visibleWhen, String? hint, String? Function(Object?, Values)? validator}) =>
      Field._(key: key, label: label, type: FieldType.decimal, required: required, min: min, max: max, decimals: decimals, initial: initial, visibleWhen: visibleWhen, hint: hint, validator: validator);

  /// An amount of money, two decimals.
  factory Field.money(String key, String label, {bool required = false, num? min = 0, num? max, Object? initial, VisibleWhen? visibleWhen, String? hint, String? Function(Object?, Values)? validator}) =>
      Field._(key: key, label: label, type: FieldType.money, required: required, min: min, max: max, decimals: 2, initial: initial, visibleWhen: visibleWhen, hint: hint, validator: validator);

  /// A date; the value is "yyyy-MM-dd". Pass `initial: 'today'` to start on today's date.
  factory Field.date(String key, String label, {bool required = false, Object? initial, VisibleWhen? visibleWhen, String? Function(Object?, Values)? validator}) =>
      Field._(key: key, label: label, type: FieldType.date, required: required, initial: initial, visibleWhen: visibleWhen, validator: validator);

  factory Field.select(String key, String label, {required List<Option> options, bool required = false, Object? initial, VisibleWhen? visibleWhen}) =>
      Field._(key: key, label: label, type: FieldType.select, options: options, required: required, initial: initial, visibleWhen: visibleWhen);

  /// Pick a record from a list endpoint (customer, item, account...). The value is its `id`.
  factory Field.lookup(String key, String label,
          {required String path,
          String labelKey = 'name',
          String? subtitleKey,
          Map<String, dynamic> query = const {},
          bool required = false,
          Object? initial,
          String? initialLabel,
          Map<String, String> copy = const {},
          VisibleWhen? visibleWhen}) =>
      Field._(
          key: key,
          label: label,
          type: FieldType.lookup,
          lookupPath: path,
          lookupLabelKey: labelKey,
          lookupSubtitleKey: subtitleKey,
          lookupQuery: query,
          required: required,
          initial: initial,
          initialLabel: initialLabel,
          copy: copy,
          visibleWhen: visibleWhen);

  factory Field.toggle(String key, String label, {bool initial = false, VisibleWhen? visibleWhen}) =>
      Field._(key: key, label: label, type: FieldType.toggle, initial: initial, visibleWhen: visibleWhen);

  /// A repeating group (document lines).
  factory Field.lines(String key, String label, {required List<Field> fields, int minLines = 1, String addLabel = 'Add line', VisibleWhen? visibleWhen}) =>
      Field._(key: key, label: label, type: FieldType.lines, lineFields: fields, minLines: minLines, addLabel: addLabel, visibleWhen: visibleWhen);

  bool get isNumeric => type == FieldType.integer || type == FieldType.decimal || type == FieldType.money;

  List<TextInputFormatter> get formatters => switch (type) {
        FieldType.integer => [FilteringTextInputFormatter.allow(RegExp(r'^-?\d*'))],
        FieldType.decimal || FieldType.money => [FilteringTextInputFormatter.allow(RegExp(r'^-?\d*\.?\d*'))],
        _ => const [],
      };
}

/// Reads a number out of what a field holds (String while typing, num after load).
num? toNum(Object? v) {
  if (v is num) return v;
  if (v is String) return num.tryParse(v.trim().replaceAll(',', ''));
  return null;
}

String todayIso() => DateFormat('yyyy-MM-dd').format(DateTime.now());

/// Builds starting values, validates, and converts form values into a request body.
class FormEngine {
  /// Starting values: each field's `initial` (with 'today' for dates), overridden by [initial].
  static Values start(List<Field> fields, [Values initial = const {}]) {
    final values = <String, dynamic>{};
    for (final f in fields) {
      if (initial.containsKey(f.key)) {
        values[f.key] = f.type == FieldType.lines ? _copyLines(initial[f.key]) : initial[f.key];
      } else if (f.type == FieldType.lines) {
        values[f.key] = <Map<String, dynamic>>[for (var i = 0; i < f.minLines; i++) start(f.lineFields)];
      } else if (f.type == FieldType.date && f.initial == 'today') {
        values[f.key] = todayIso();
      } else if (f.initial != null) {
        values[f.key] = f.initial;
      }
      if (f.type == FieldType.lookup) {
        final label = initial['__label_${f.key}'] ?? f.initialLabel;
        if (label != null) values['__label_${f.key}'] = label;
      }
    }
    return values;
  }

  static List<Map<String, dynamic>> _copyLines(Object? raw) => [for (final l in (raw as List<dynamic>? ?? const [])) Map<String, dynamic>.from(l as Map)];

  /// A message for the first problem with [value], or null.
  static String? check(Field f, Object? value, Values values) {
    final empty = value == null || (value is String && value.trim().isEmpty);
    if (f.required && empty) return '${f.label} is required';
    if (!empty && f.isNumeric) {
      final n = toNum(value);
      if (n == null) return 'Enter a number';
      if (f.min != null && n < f.min!) return 'Must be at least ${f.min}';
      if (f.max != null && n > f.max!) return 'Must be at most ${f.max}';
      if (f.type == FieldType.integer && n != n.truncate()) return 'Enter a whole number';
      final places = f.decimals;
      if ((f.type == FieldType.decimal || f.type == FieldType.money) && (n * _pow10(places)).round() / _pow10(places) != n) return 'At most $places decimal places';
    }
    if (f.type == FieldType.lines && f.minLines > 0 && ((value as List?)?.length ?? 0) < f.minLines) return 'Add at least ${f.minLines} line${f.minLines == 1 ? '' : 's'}';
    return f.validator?.call(value, values);
  }

  static num _pow10(int n) => n == 0 ? 1 : (n == 1 ? 10 : (n == 2 ? 100 : (n == 3 ? 1000 : 10000)));

  /// The request body: numbers parsed, empty optional values dropped, display labels removed, lines converted the same way.
  static Values collect(List<Field> fields, Values values) {
    final body = <String, dynamic>{};
    for (final f in fields) {
      if (f.visibleWhen != null && !f.visibleWhen!(values)) continue;
      final v = values[f.key];
      switch (f.type) {
        case FieldType.lines:
          body[f.key] = [for (final line in (v as List<dynamic>? ?? const [])) collect(f.lineFields, line as Values)];
        case FieldType.toggle:
          body[f.key] = v == true;
        case FieldType.integer || FieldType.decimal || FieldType.money:
          final n = toNum(v);
          if (n != null) body[f.key] = n;
        default:
          if (v == null || (v is String && v.trim().isEmpty)) continue;
          body[f.key] = v is String ? v.trim() : v;
      }
    }
    return body;
  }

  /// Validates every visible field (and every line); returns key -> message, with line errors as "lines[0].price".
  static Map<String, String> validate(List<Field> fields, Values values) {
    final errors = <String, String>{};
    for (final f in fields) {
      if (f.visibleWhen != null && !f.visibleWhen!(values)) continue;
      final message = check(f, values[f.key], values);
      if (message != null) errors[f.key] = message;
      if (f.type == FieldType.lines) {
        final lines = (values[f.key] as List<dynamic>? ?? const []);
        for (var i = 0; i < lines.length; i++) {
          validate(f.lineFields, lines[i] as Values).forEach((k, m) => errors['${f.key}[$i].$k'] = m);
        }
      }
    }
    return errors;
  }
}

/// Small helpers for building labels in forms and lists.
Widget fieldGap([double h = 12]) => SizedBox(height: h);
