import 'package:erp_mobile/core/format.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Paged knows whether another page follows', () {
    final first = Paged.fromJson({'items': [<String, dynamic>{}, <String, dynamic>{}], 'total': 5, 'page': 1, 'pageSize': 2});
    final last = Paged.fromJson({'items': [<String, dynamic>{}], 'total': 5, 'page': 3, 'pageSize': 2});
    expect(first.hasNext, isTrue);
    expect(last.hasNext, isFalse);
  });

  test('Dashboard parses KPIs, charts and alerts', () {
    final d = Dashboard.fromJson({
      'asOf': '2026-10-30',
      'kpis': [
        {'key': 'ar', 'label': 'Receivables', 'value': 1234.5, 'format': 'money', 'caption': 'Open invoices'}
      ],
      'charts': [
        {
          'key': 'rev',
          'title': 'Revenue',
          'kind': 'bar',
          'categories': ['Aug', 'Sep'],
          'series': [
            {'name': '2026', 'values': [10, 20.5]}
          ]
        }
      ],
      'alerts': [
        {'severity': 'critical', 'category': 'Cheques', 'message': '3 cheques bounced'}
      ],
    });
    expect(d.kpis.single.value, 1234.5);
    expect(d.charts.single.series.single.values, [10.0, 20.5]);
    expect(d.alerts.single.severity, 'critical');
  });

  test('SessionInfo parses the workspaces of the account', () {
    final s = SessionInfo.fromJson({
      'accessToken': 't',
      'expiresIn': 900,
      'workspace': {'id': 'w1', 'code': 'A', 'name': 'Acme', 'industry': 'trading'},
      'workspaces': [
        {'id': 'w1', 'code': 'A', 'name': 'Acme', 'industry': 'trading'},
        {'id': 'w2', 'code': 'B', 'name': 'Beta', 'industry': 'x'}
      ],
    });
    expect(s.workspace?.name, 'Acme');
    expect(s.workspaces.map((w) => w.code), ['A', 'B']);
  });

  group('format', () {
    test('money has thousands separators and two decimals', () {
      expect(formatMoney(1234.5), '1,234.50');
      expect(formatMoney(0), '0.00');
    });

    test('KPI values follow their declared format', () {
      expect(formatKpi(1234.5, 'money'), '1,234.50');
      expect(formatKpi(12.5, 'percent'), '12.5%');
      expect(formatKpi(1200, 'number'), '1,200');
    });

    test('dates are shown short and bad input is left alone', () {
      expect(formatDate('2026-10-30'), '30 Oct 2026');
      expect(formatDate('soon'), 'soon');
      expect(formatDate(null), '');
    });

    test('camel-case statuses become words', () {
      expect(humanize('partlyPaid'), 'Partly paid');
      expect(humanize('paid'), 'Paid');
      expect(humanize(null), '');
    });
  });
}
