import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/receivables/invoice_screens.dart';
import 'package:erp_mobile/features/receivables/receipt_screens.dart';
import 'package:erp_mobile/kit/pdf_share.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(
  fullName: 'A',
  email: 'a@x',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'LE', name: 'Main', baseCurrency: 'QAR')],
  modules: {},
);

class FakePdfOutput implements PdfOutput {
  final calls = <(String, Uint8List, String)>[];
  Object? failWith;

  @override
  Future<void> printPdf(Uint8List bytes, String fileName) async {
    if (failWith != null) throw failWith!;
    calls.add(('print', bytes, fileName));
  }

  @override
  Future<void> sharePdf(Uint8List bytes, String fileName) async {
    if (failWith != null) throw failWith!;
    calls.add(('share', bytes, fileName));
  }
}

final _pdf = Uint8List.fromList(utf8.encode('%PDF-1.7 fake'));

ResponseBody _pdfBody(String fileName) => ResponseBody.fromBytes(_pdf, 200, headers: {
      Headers.contentTypeHeader: ['application/pdf'],
      'content-disposition': ['attachment; filename=$fileName; filename*=UTF-8\'\'$fileName'],
    });

const _invoice = {
  'id': 'i1', 'number': 'INV-0007', 'isCreditNote': false, 'status': 'issued', 'partyId': 'p1', 'partyName': 'Acme',
  'invoiceDate': '2026-06-01', 'dueDate': '2026-07-01', 'currencyCode': 'QAR', 'net': 100, 'tax': 0, 'total': 100, 'paid': 0, 'lines': [],
};
const _receipt = {
  'id': 'r1', 'number': 'RCT-0003', 'status': 'issued', 'partyId': 'p1', 'partyName': 'Acme', 'date': '2026-06-05',
  'method': 'bankTransfer', 'currencyCode': 'QAR', 'amount': 100, 'unallocated': 0, 'allocations': [],
};

Widget _host(FakeAdapter adapter, FakePdfOutput output, Widget child) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meProvider.overrideWith((ref) async => _me),
      pdfOutputProvider.overrideWithValue(output),
    ],
    child: MaterialApp(home: child),
  );
}

FakeAdapter _server({ResponseBody Function()? pdf}) => FakeAdapter((o) {
      if (o.path.endsWith('/pdf')) return pdf?.call() ?? _pdfBody('Invoice-INV-0007.pdf');
      if (o.path.contains('/receipts/')) return envelope(_receipt);
      if (o.path.contains('/invoices/')) return envelope(_invoice);
      return envelope({'items': [], 'total': 0});
    });

Future<void> _choose(WidgetTester t, String option) async {
  await t.tap(find.byTooltip('Print / Share PDF'));
  await t.pumpAndSettle();
  await t.tap(find.text(option));
  await t.pumpAndSettle();
}

void main() {
  testWidgets('invoice: Share downloads the server PDF and hands it to the share sheet with the server file name', (t) async {
    final adapter = _server();
    final output = FakePdfOutput();
    await t.pumpWidget(_host(adapter, output, const InvoiceDetailScreen(id: 'i1')));
    await t.pumpAndSettle();

    await _choose(t, 'Share PDF');

    final pdfCall = adapter.requests.singleWhere((r) => r.path.endsWith('/pdf'));
    expect(pdfCall.path, '/api/v1/print/invoice/i1/pdf');
    expect(pdfCall.responseType, ResponseType.bytes);
    expect(output.calls, hasLength(1));
    expect(output.calls.single.$1, 'share');
    expect(output.calls.single.$2, _pdf);
    expect(output.calls.single.$3, 'Invoice-INV-0007.pdf');
  });

  testWidgets('receipt: Print sends the server PDF to the print dialog', (t) async {
    final adapter = _server(pdf: () => _pdfBody('Receipt-RCT-0003.pdf'));
    final output = FakePdfOutput();
    await t.pumpWidget(_host(adapter, output, const ReceiptDetailScreen(id: 'r1')));
    await t.pumpAndSettle();

    await _choose(t, 'Print');

    expect(adapter.requests.where((r) => r.path == '/api/v1/print/receipt/r1/pdf'), hasLength(1));
    expect(output.calls.single.$1, 'print');
    expect(output.calls.single.$3, 'Receipt-RCT-0003.pdf');
  });

  testWidgets('a server error shows its message and nothing is shared', (t) async {
    final adapter = _server(pdf: () => problem(404, 'Receipt not found.'));
    final output = FakePdfOutput();
    await t.pumpWidget(_host(adapter, output, const ReceiptDetailScreen(id: 'r1')));
    await t.pumpAndSettle();

    await _choose(t, 'Share PDF');

    expect(find.text('Receipt not found.'), findsOneWidget);
    expect(output.calls, isEmpty);
    expect(find.byTooltip('Print / Share PDF'), findsOneWidget); // usable again
  });

  testWidgets('a failing print/share sheet shows a friendly error', (t) async {
    final output = FakePdfOutput()..failWith = Exception('no print service');
    await t.pumpWidget(_host(_server(), output, const InvoiceDetailScreen(id: 'i1')));
    await t.pumpAndSettle();

    await _choose(t, 'Print');

    expect(find.text("Couldn't open the print or share sheet."), findsOneWidget);
  });

  testWidgets('falls back to a generated file name when the server sends none', (t) async {
    final adapter = _server(pdf: () => ResponseBody.fromBytes(_pdf, 200, headers: {Headers.contentTypeHeader: ['application/pdf']}));
    final output = FakePdfOutput();
    await t.pumpWidget(_host(adapter, output, const InvoiceDetailScreen(id: 'i1')));
    await t.pumpAndSettle();

    await _choose(t, 'Share PDF');

    expect(output.calls.single.$3, 'Invoice-INV-0007.pdf');
  });

  test('getBytes decodes JSON error bodies into ApiException and reads the file name', () async {
    final store = MemoryKeyValueStore();
    final ok = ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = FakeAdapter((_) => _pdfBody('Bill-B-1.pdf')));
    final file = await ok.getBytes('/x');
    expect(file.bytes, _pdf);
    expect(file.fileName, 'Bill-B-1.pdf');

    final bad = ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = FakeAdapter((_) => problem(403, 'Not allowed.')));
    expect(() => bad.getBytes('/x'), throwsA(isA<ApiException>().having((e) => e.status, 'status', 403).having((e) => e.message, 'message', 'Not allowed.')));
  });

  test('parseFileName handles quoted, plain and RFC 5987 names', () {
    expect(ApiClient.parseFileName('attachment; filename="A B.pdf"'), 'A B.pdf');
    expect(ApiClient.parseFileName('attachment; filename=Invoice-1.pdf'), 'Invoice-1.pdf');
    expect(ApiClient.parseFileName("attachment; filename=x.pdf; filename*=UTF-8''Invoice%20%C3%A9.pdf"), 'Invoice é.pdf');
    expect(ApiClient.parseFileName(null), isNull);
  });
}
