import 'package:intl/intl.dart';

final _money = NumberFormat('#,##0.00');
final _number = NumberFormat('#,##0.##');

/// 1234.5 -> "1,234.50"
String formatMoney(num value) => _money.format(value);

/// A dashboard figure by its declared format: "money", "percent" or "number".
String formatKpi(double value, String format) => switch (format) {
      'money' => _money.format(value),
      'percent' => '${_number.format(value)}%',
      _ => _number.format(value),
    };

/// "2026-10-30" -> "30 Oct 2026"; anything that is not a date is returned as it came.
String formatDate(Object? value) {
  final parsed = DateTime.tryParse(value?.toString() ?? '');
  return parsed == null ? (value?.toString() ?? '') : DateFormat('d MMM yyyy').format(parsed);
}

/// "partlyPaid" -> "Partly paid"
String humanize(String? camel) {
  if (camel == null || camel.isEmpty) return '';
  final spaced = camel.replaceAllMapped(RegExp(r'(?<=[a-z])(?=[A-Z])'), (_) => ' ').toLowerCase();
  return spaced[0].toUpperCase() + spaced.substring(1);
}
