/// The arithmetic of the till, a line-for-line port of `frontend/src/app/pages/pos/pos-cart.ts`, and with it of the server's decimal
/// arithmetic (`PosMath`, `ReturnFeatures`). The server refuses a payment or a refund that differs by a cent, so keep the three in step.
library;

/// Reads a number out of JSON or typed text.
double _d(Object? v) => v is num ? v.toDouble() : (v is String ? double.tryParse(v) ?? 0 : 0);

double? _dOrNull(Object? v) => v == null ? null : _d(v);

/// To cents, halves away from zero, as the server's decimal arithmetic does. Binary floats hold 14.50 x 15% as 2.17499..., so the product
/// is cleaned of that dust before rounding; otherwise the till asks for a cent less than the server charges and the payment is refused.
double round2(num n) {
  final cents = double.parse((n.abs() * 100).toStringAsFixed(6)).roundToDouble();
  final rounded = (n < 0 ? -cents : cents) / 100;
  return rounded == 0 ? 0.0 : rounded; // never -0.0
}

/// JavaScript's Number.EPSILON, which the web's rounding adds and Dart does not name.
const _epsilon = 2.220446049250313e-16;

/// Quantities keep the four decimals the database holds.
double roundQty(num n) => ((n + _epsilon) * 10000).roundToDouble() / 10000;

const walkInCustomer = 'Walk-in Customer';

class CartLine {
  const CartLine({
    required this.key,
    required this.itemId,
    required this.itemCode,
    required this.description,
    required this.quantity,
    required this.unitPrice,
    required this.listPrice,
    this.discountPercent = 0,
    this.taxRate = 0,
    this.onHand,
  });

  /// Stable key on the ticket: the item id, or a generated one for a typed-in item.
  final String key;
  final String? itemId;
  final String itemCode;
  final String description;
  final double quantity;
  final double unitPrice;

  /// The price the catalogue gave. A different [unitPrice] is an override the server checks.
  final double? listPrice;
  final double discountPercent;
  final double taxRate;
  final double? onHand;

  CartLine copyWith({double? quantity, double? discountPercent, double? onHand}) => CartLine(
        key: key,
        itemId: itemId,
        itemCode: itemCode,
        description: description,
        quantity: quantity ?? this.quantity,
        unitPrice: unitPrice,
        listPrice: listPrice,
        discountPercent: discountPercent ?? this.discountPercent,
        taxRate: taxRate,
        onHand: onHand ?? this.onHand,
      );
}

class LineAmounts {
  const LineAmounts({required this.gross, required this.discount, required this.net, required this.tax, required this.total});

  final double gross;
  final double discount;
  final double net;
  final double tax;
  final double total;
}

/// Rounded exactly as the server does it, line by line, so the amount sent always equals the order total.
LineAmounts lineAmounts({required num quantity, required num unitPrice, num discountPercent = 0, num taxRate = 0}) {
  final gross = round2(quantity * unitPrice);
  final discount = round2(gross * (discountPercent / 100));
  final net = round2(gross - discount);
  final tax = round2(net * (taxRate / 100));
  return LineAmounts(gross: gross, discount: discount, net: net, tax: tax, total: round2(net + tax));
}

LineAmounts amountsOf(CartLine l) => lineAmounts(quantity: l.quantity, unitPrice: l.unitPrice, discountPercent: l.discountPercent, taxRate: l.taxRate);

class CartTotals {
  const CartTotals({required this.gross, required this.discount, required this.net, required this.tax, required this.total, required this.units});

  final double gross;
  final double discount;
  final double net;
  final double tax;
  final double total;
  final double units;
}

CartTotals cartTotals(Iterable<CartLine> lines) {
  double gross = 0, discount = 0, net = 0, tax = 0, units = 0;
  for (final l in lines) {
    final a = amountsOf(l);
    gross += a.gross;
    discount += a.discount;
    net += a.net;
    tax += a.tax;
    units += l.quantity;
  }
  net = round2(net);
  tax = round2(tax);
  return CartTotals(gross: round2(gross), discount: round2(discount), net: net, tax: tax, total: round2(net + tax), units: round2(units));
}

/// What the till sends. The price goes only when it differs from the catalogue, so the server prices everything else itself.
Map<String, dynamic> toOrderLine(CartLine l) {
  if (l.itemId == null) {
    return {'itemId': null, 'itemCode': l.itemCode, 'description': l.description, 'quantity': l.quantity, 'unitPrice': l.unitPrice, 'discountPercent': l.discountPercent, 'taxRate': l.taxRate};
  }
  return {'itemId': l.itemId, 'quantity': l.quantity, 'discountPercent': l.discountPercent, if (l.unitPrice != l.listPrice) 'unitPrice': l.unitPrice};
}

/// A catalogue item (`GET /terminal/catalog`) as a new ticket line.
CartLine lineFromCatalog(Map<String, dynamic> item, double quantity) => CartLine(
      key: item['id'].toString(),
      itemId: item['id'].toString(),
      itemCode: '${item['sku']}',
      description: '${item['name']}',
      quantity: quantity,
      unitPrice: _d(item['price']),
      listPrice: _d(item['price']),
      taxRate: _d(item['taxRate']),
      onHand: _dOrNull(item['onHand']),
    );

/// A held ticket back on the till, at the prices it was held at.
List<CartLine> fromHeldOrder(Map<String, dynamic> order) => [
      for (final l in (order['lines'] as List<dynamic>).cast<Map<String, dynamic>>())
        CartLine(
          key: l['itemId']?.toString() ?? 'open-${l['id']}',
          itemId: l['itemId']?.toString(),
          itemCode: '${l['itemCode']}',
          description: '${l['description']}',
          quantity: _d(l['quantity']),
          unitPrice: _d(l['unitPrice']),
          listPrice: null, // not known until repriceHeld has looked it up; until then the held price is sent as it is
          discountPercent: _d(l['discountPercent']),
          taxRate: _d(l['taxRate']),
        ),
    ];

/// Brings a held line up to today's catalogue. A price set by hand stays for someone who may set prices; for anyone else the server
/// would charge the catalogue price anyway, so the ticket shows that. An item no longer on sale stays as it is and the server explains at payment.
CartLine repriceHeld(CartLine line, Map<String, dynamic>? item, {required bool canOverridePrice}) {
  if (item == null) return line;
  final price = _dOrNull(item['price']);
  return CartLine(
    key: line.key,
    itemId: line.itemId,
    itemCode: line.itemCode,
    description: line.description,
    quantity: line.quantity,
    unitPrice: canOverridePrice || price == null ? line.unitPrice : price,
    listPrice: price,
    discountPercent: line.discountPercent,
    taxRate: _d(item['taxRate']),
    onHand: _dOrNull(item['onHand']),
  );
}

/// What a return of this quantity gives back: its running share of what the line charged, less what earlier returns took.
/// The same arithmetic as the server (ReturnFeatures), so the refund it asks for is always the one the server accepts.
double returnAmount(Map<String, dynamic> line, num quantity) {
  if (quantity <= 0) return 0;
  final sold = _d(line['quantity']);
  final net = lineAmounts(quantity: sold, unitPrice: _d(line['unitPrice']), discountPercent: _d(line['discountPercent']), taxRate: _d(line['taxRate'])).net;
  final tax = round2(_d(line['lineTotal']) - net);
  final soFar = _d(line['returnedQuantity']) + quantity;
  return round2(round2((net * soFar) / sold) + round2((tax * soFar) / sold) - _d(line['returnedAmount']));
}

/// Money goes back the way it came: what the chosen way cannot take goes the other way, so a sale paid part on account can be returned in full.
List<Map<String, dynamic>> splitRefund(double total, String method, double moneyLeft, double onAccountLeft, String moneyMethod, String? reference) {
  if (onAccountLeft <= 0) {
    return [
      {'method': method, 'amount': total, 'reference': reference},
    ];
  }
  final account = method == 'account' ? (total < onAccountLeft ? total : onAccountLeft) : round2(total - (total < moneyLeft ? total : moneyLeft));
  final money = round2(total - account);
  return [
    if (money > 0) {'method': method == 'account' ? moneyMethod : method, 'amount': money, 'reference': reference},
    if (account > 0) {'method': 'account', 'amount': account, 'reference': null},
  ];
}

/// The refund ways offered for a sale: cash and card always, plus how it was actually paid. On account only when the sale made an invoice.
List<String> refundMethods(Map<String, dynamic> order) {
  final paid = _paymentMethods(order).where((m) => m != 'giftCard');
  final methods = <String>{'cash', 'card', ...paid};
  if (order['invoiceId'] != null) {
    methods.add('account');
  } else {
    methods.remove('account');
  }
  return methods.toList();
}

/// Back the way it was paid: on account when it was, else the first tender.
String defaultRefundMethod(Map<String, dynamic> order) {
  if (order['invoiceId'] != null) return 'account';
  final first = _paymentMethods(order).firstOrNull;
  return first != null && first != 'giftCard' ? first : 'cash';
}

/// The first way this sale was paid other than on account, for the part of a refund the account cannot take.
String moneyRefundMethod(Map<String, dynamic> order) => _paymentMethods(order).where((m) => m != 'account' && m != 'giftCard').firstOrNull ?? 'cash';

Iterable<String> _paymentMethods(Map<String, dynamic> order) => (order['payments'] as List<dynamic>? ?? const []).map((p) => '${(p as Map)['method']}');

class ScanInput {
  const ScanInput(this.quantity, this.code);

  final double quantity;
  final String code;
}

final _quantityPrefix = RegExp(r'^(\d+(?:\.\d+)?)\s*[*x×]\s*(.+)$', caseSensitive: false);

/// "3*ABC" sells three of ABC. Anything else is one of what was typed or scanned.
ScanInput parseScan(String text) {
  final value = text.trim();
  final match = _quantityPrefix.firstMatch(value);
  if (match != null) {
    final quantity = roundQty(double.parse(match.group(1)!));
    if (quantity > 0) return ScanInput(quantity, match.group(2)!.trim());
  }
  return ScanInput(1, value);
}

/// The next round amounts a customer is likely to hand over.
List<double> quickCash(double due) {
  if (due <= 0) return const [];
  final rounded = <double>{for (final step in const [5, 10, 50, 100, 500, 1000]) ((due / step - 1e-9).ceil() * step).toDouble()}.where((n) => n > due).toList()..sort();
  return rounded.take(4).toList();
}

/// One tender as the cashier entered it. Cash may be more than is due: the difference is change.
class Tender {
  const Tender(this.method, this.amount, [this.reference]);

  final String method;
  final double amount;
  final String? reference;
}

double tenderedTotal(Iterable<Tender> tenders) => round2(tenders.fold<double>(0, (sum, t) => sum + t.amount));

double remainingDue(double total, Iterable<Tender> tenders) {
  final left = round2(total - tenderedTotal(tenders));
  return left > 0 ? left : 0;
}

/// Only cash can give change. A card or account amount above what is due is a mistake, not change.
double changeDue(double total, Iterable<Tender> tenders) {
  final over = round2(tenderedTotal(tenders) - total);
  if (over <= 0) return 0;
  final cash = tenders.where((t) => t.method == 'cash').fold<double>(0, (sum, t) => sum + t.amount);
  return round2(over < cash ? over : cash);
}

/// True when the tenders cover the total and nothing but cash goes over it.
bool tendersSettle(double total, List<Tender> tenders) {
  if (tenders.isEmpty) return false;
  final over = round2(tenderedTotal(tenders) - total);
  return over >= 0 && over == changeDue(total, tenders);
}

/// Payments for the server. Each records the amount applied, so change comes off the cash and the sum equals the total.
List<Map<String, dynamic>> toPayments(double total, List<Tender> tenders) {
  var change = changeDue(total, tenders);
  final payments = <Map<String, dynamic>>[];
  for (final t in tenders) {
    if (t.method != 'cash') {
      payments.add({'method': t.method, 'amount': t.amount, 'tenderedAmount': t.amount, 'changeAmount': 0.0, 'reference': (t.reference ?? '').isEmpty ? null : t.reference});
      continue;
    }
    final given = change < t.amount ? change : t.amount;
    change = round2(change - given);
    payments.add({'method': 'cash', 'amount': round2(t.amount - given), 'tenderedAmount': t.amount, 'changeAmount': given, 'reference': null});
  }
  return payments.where((p) => (p['amount'] as double) > 0).toList();
}

/// The largest discount this person may give, as the server works it out (pos.maxDiscountPercent, pos.cashierDiscountPercent).
double discountLimit(Map<String, dynamic>? settings, {required bool canOverride}) {
  if (settings == null) return 100;
  final max = _d(settings['maxDiscountPercent']);
  final cashier = _dOrNull(settings['cashierDiscountPercent']);
  return cashier != null && !canOverride && cashier < max ? cashier : max;
}

const tenderLabels = {'cash': 'Cash', 'card': 'Card', 'other': 'Other', 'account': 'On account', 'giftCard': 'Gift card'};
