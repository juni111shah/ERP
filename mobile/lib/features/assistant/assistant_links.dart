import 'package:flutter/widgets.dart';

import '../../core/me.dart';
import '../dashboard/overview_logic.dart';
import '../ops/inventory_screens.dart' show CountDetailScreen;
import '../ops/pos_screens.dart' as pos show OrderDetailScreen;
import '../payables/bill_screens.dart' show BillDetailScreen;
import '../projects/projects_screens.dart' show ProjectDetailScreen;
import '../purchasing/order_screens.dart' as purchasing show OrderDetailScreen;
import '../realestate/lease_screens.dart' show LeaseDetailScreen;
import '../realestate/property_screens.dart' show PropertyDetailScreen;
import '../receivables/invoice_screens.dart' show InvoiceDetailScreen;
import '../receivables/party_screens.dart' show PartyDetailScreen;
import '../sales/order_screens.dart' as sales show OrderDetailScreen;
import '../sales/quote_screens.dart' show QuoteDetailScreen;
import 'assistant_text.dart';

/// The web's record addresses (as the server's assistant writes them) that have a detail screen in the app.
final Map<String, Widget Function(String id)> _detailScreens = {
  'parties': (id) => PartyDetailScreen(id: id),
  'finance/invoices': (id) => InvoiceDetailScreen(id: id),
  'finance/bills': (id) => BillDetailScreen(id: id),
  'sales/quotes': (id) => QuoteDetailScreen(id: id),
  'sales/orders': (id) => sales.OrderDetailScreen(id: id),
  'purchasing/orders': (id) => purchasing.OrderDetailScreen(id: id),
  'pos/orders': (id) => pos.OrderDetailScreen(id: id),
  'inventory/counts': (id) => CountDetailScreen(id: id),
  'projects': (id) => ProjectDetailScreen(id: id),
  'real-estate/properties': (id) => PropertyDetailScreen(id: id),
  'real-estate/leases': (id) => LeaseDetailScreen(id: id),
};

/// A record's id: a number or a GUID, so that a word such as "new" is never taken for one or put into an API path.
final _recordId = RegExp(r'^(\d+|[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})$');

/// What a link in the assistant's words opens: the record's own screen, or else the list it belongs to. Null when the app has neither,
/// or when the address is not on this site.
WidgetBuilder? assistantLinkTarget(String? link, {List<MenuGroup> menu = const []}) {
  if (link == null || !isInternalLink(link)) return null;
  final segments = [for (final s in (Uri.tryParse(link)?.path ?? link).split('/')) if (s.isNotEmpty) s];
  if (segments.length >= 2 && _recordId.hasMatch(segments.last)) {
    final id = segments.last;
    final list = segments.take(segments.length - 1).join('/').toLowerCase();
    final detail = _detailScreens[list];
    if (detail != null) return (_) => detail(id);
    return screenForLink('/$list', menu: menu)?.builder;
  }
  return screenForLink(link, menu: menu)?.builder;
}
