import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/me.dart';
import '../../kit/field.dart';

const finPath = '/api/v1/finance';

const docStatuses = [
  Option('draft', 'Draft'),
  Option('pendingApproval', 'Pending approval'),
  Option('issued', 'Posted'),
  Option('partlyPaid', 'Partly paid'),
  Option('paid', 'Paid'),
  Option('voided', 'Voided'),
];

/// Document status shown to the user (payables call an issued document "posted").
String docStatusLabel(String? s) {
  if (s == null || s.isEmpty) return '';
  if (s == 'issued') return 'Posted';
  final spaced = s.replaceAllMapped(RegExp(r'(?<=[a-z])(?=[A-Z])'), (_) => ' ').toLowerCase();
  return spaced[0].toUpperCase() + spaced.substring(1);
}

Field entityField(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id);
}

Field vendorField({String key = 'partyId'}) =>
    Field.lookup(key, 'Vendor', path: '/api/v1/parties', query: const {'role': 'Vendor'}, subtitleKey: 'code', required: true);

Field accountField(String key, String label, {bool required = true, bool bankOnly = false}) => Field.lookup(key, label,
    path: '$finPath/accounts', subtitleKey: 'code', required: required, query: bankOnly ? const {'filter': 'type:eq:asset'} : const {});

Field taxCodeField([String key = 'taxCodeId']) => Field.lookup(key, 'Tax code (optional)', path: '$finPath/tax-codes', subtitleKey: 'code');

/// Dates the server accepts: after 2000, at most one year ahead.
String? plausibleDate(Object? v, Values all) {
  final d = DateTime.tryParse(v?.toString() ?? '');
  if (d == null) return null;
  if (d.year < 2000) return 'Enter a date after 2000';
  if (d.isAfter(DateTime.now().add(const Duration(days: 366)))) return 'At most one year ahead';
  return null;
}

String? Function(Object?, Values) notBefore(String otherKey, String message) => (v, all) {
      final a = DateTime.tryParse(v?.toString() ?? '');
      final b = DateTime.tryParse(all[otherKey]?.toString() ?? '');
      if (a != null && b != null && a.isBefore(b)) return message;
      return plausibleDate(v, all);
    };

num lineTotal(Map l) => (toNum(l['quantity']) ?? 0) * (toNum(l['price']) ?? 0);

void pushPage(BuildContext context, Widget page) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => page));
