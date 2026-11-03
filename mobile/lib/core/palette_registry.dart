import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/home/profile_sheet.dart';
import '../features/notifications/notifications.dart';
import '../kit/command_palette.dart';
import '../kit/registry.dart';
import '../screens/all_screens.dart';

/// What choosing a palette entry does. Kept in [PaletteEntry.data].
sealed class PaletteTarget {
  const PaletteTarget();
}

/// A go_router location (only '/home' and '/settings' exist in app.dart today).
class RouteTarget extends PaletteTarget {
  const RouteTarget(this.path, {this.push = true});
  final String path;

  /// push (keeps back navigation) vs go (replaces the stack).
  final bool push;
}

/// A bottom tab of the home shell ('/home'); tabs are an index in HomePage state, not routes.
class TabTarget extends PaletteTarget {
  const TabTarget(this.index);
  final int index;
}

/// A [MobileScreen] from `allScreens`, opened the same way the More grid does (Navigator.push, no go_router path).
class ScreenTarget extends PaletteTarget {
  const ScreenTarget(this.screen);
  final MobileScreen screen;
}

enum PaletteAction { notifications, appearance }

/// Something that opens a sheet rather than a page.
class ActionTarget extends PaletteTarget {
  const ActionTarget(this.action);
  final PaletteAction action;
}

/// Home tab indices, in HomePage order.
abstract final class HomeTabs {
  static const overview = 0;
  static const invoices = 1;
  static const receipts = 2;
  static const customers = 3;
  static const more = 4;
}

typedef _Meta = ({String section, String subtitle, List<String> keywords});

/// Plain-language subtitle, area and search words for every screen id in `allScreens`.
const Map<String, _Meta> _screenMeta = {
  // Sales & receivables
  'vendors': (section: 'Purchasing', subtitle: 'Suppliers you buy from, with contacts and balances', keywords: ['suppliers', 'payee', 'parties']),
  'parties': (section: 'Finance', subtitle: 'Every customer and vendor in one list', keywords: ['contacts', 'customers', 'vendors', 'companies']),
  'partygroups': (section: 'Finance', subtitle: 'Groups used to organise customers and vendors', keywords: ['segments', 'categories', 'party groups']),
  'items': (section: 'Inventory', subtitle: 'Products and services you buy or sell, with prices', keywords: ['products', 'sku', 'catalog', 'services']),
  'ar-ageing': (section: 'Finance', subtitle: 'Who owes you money and how overdue it is', keywords: ['aging', 'overdue', 'receivables', 'debtors', 'ar']),
  // Payables & finance
  'bills': (section: 'Finance', subtitle: 'Supplier invoices you need to pay', keywords: ['payables', 'ap', 'purchase invoice', 'vendor bill']),
  'payments': (section: 'Finance', subtitle: 'Money you paid out to suppliers', keywords: ['pay', 'outgoing', 'vendor payment', 'ap']),
  'journals': (section: 'Finance', subtitle: 'Manual accounting entries and adjustments', keywords: ['journal entry', 'je', 'ledger', 'gl']),
  'cheques': (section: 'Finance', subtitle: 'Post-dated and issued cheques, and their status', keywords: ['checks', 'pdc', 'cheque register']),
  'bank': (section: 'Finance', subtitle: 'Cash and bank balances by account', keywords: ['cash', 'bank accounts', 'balance', 'treasury']),
  'budgets': (section: 'Finance', subtitle: 'Planned spending compared with actuals', keywords: ['plan', 'forecast', 'vs actual']),
  'reports': (section: 'Finance', subtitle: 'Profit and loss, balance sheet and other reports', keywords: ['p&l', 'pnl', 'balance sheet', 'trial balance', 'statements']),
  'accounts': (section: 'Finance', subtitle: 'The list of ledger accounts', keywords: ['coa', 'chart of accounts', 'gl', 'ledger']),
  'taxcodes': (section: 'Finance', subtitle: 'VAT and other tax rates used on documents', keywords: ['vat', 'tax', 'rates', 'gst']),
  'approvals': (section: 'Finance', subtitle: 'Documents waiting for your approval', keywords: ['approve', 'pending', 'inbox', 'workflow']),
  // Sales
  'sales-quotes': (section: 'Sales', subtitle: 'Price quotes sent to customers', keywords: ['quotation', 'estimate', 'proposal']),
  'sales-orders': (section: 'Sales', subtitle: 'Orders customers have confirmed', keywords: ['so', 'order', 'booking']),
  'retail-stores': (section: 'Retail', subtitle: 'Your shops and the warehouse each sells from', keywords: ['shops', 'branches', 'outlets']),
  'retail-pricelists': (section: 'Retail', subtitle: 'Selling prices per store or customer group', keywords: ['prices', 'pricing', 'price list']),
  'retail-promotions': (section: 'Retail', subtitle: 'Discounts and offers', keywords: ['discount', 'offer', 'deal', 'promo']),
  'distribution-routes': (section: 'Distribution', subtitle: 'Delivery routes and the customers on them', keywords: ['delivery', 'van', 'route']),
  'distribution-trips': (section: 'Distribution', subtitle: 'Van sales trips: load, sell and settle', keywords: ['trips', 'van', 'driver', 'delivery']),
  'distribution-wholesale': (section: 'Distribution', subtitle: 'Bulk prices for wholesale customers', keywords: ['bulk', 'wholesale', 'pricing']),
  // Operations
  'pos.terminal': (section: 'POS', subtitle: 'Ring up a sale at the counter', keywords: ['point of sale', 'checkout', 'till', 'sell']),
  'pos.sessions': (section: 'POS', subtitle: 'Open and close cash drawer shifts', keywords: ['shift', 'drawer', 'session', 'cash up']),
  'pos.orders': (section: 'POS', subtitle: 'Sales made at the counter', keywords: ['pos orders', 'counter', 'receipts']),
  'pos.registers': (section: 'POS', subtitle: 'Tills and devices used for counter sales', keywords: ['till', 'register', 'device']),
  'pos.analytics': (section: 'POS', subtitle: 'Counter sales trends and best sellers', keywords: ['pos report', 'sales analytics']),
  'inventory.stock': (section: 'Inventory', subtitle: 'How much of each item you have, by warehouse', keywords: ['stock', 'on hand', 'quantity', 'soh']),
  'inventory.movements': (section: 'Inventory', subtitle: 'Stock moving in, out and between warehouses', keywords: ['transfer', 'movement', 'stock in', 'stock out']),
  'inventory.counts': (section: 'Inventory', subtitle: 'Count stock and fix differences', keywords: ['stocktake', 'cycle count', 'physical count']),
  'inventory.warehouses': (section: 'Inventory', subtitle: 'Places where you keep stock', keywords: ['warehouse', 'location', 'store room']),
  'manufacturing.boms': (section: 'Manufacturing', subtitle: 'Recipes: the parts that make each product', keywords: ['bom', 'recipe', 'components']),
  'manufacturing.workorders': (section: 'Manufacturing', subtitle: 'Production jobs on the shop floor', keywords: ['production', 'job', 'work order', 'mo']),
  'manufacturing.costing': (section: 'Manufacturing', subtitle: 'What each product costs to make', keywords: ['cost', 'product cost', 'margin']),
  'manufacturing.analytics': (section: 'Manufacturing', subtitle: 'Production output and efficiency', keywords: ['manufacturing report', 'output']),
  // Purchasing
  'purchasing-orders': (section: 'Purchasing', subtitle: 'Orders you placed with suppliers', keywords: ['po', 'purchase order', 'buy']),
  'purchasing-requisitions': (section: 'Purchasing', subtitle: 'Requests to buy something, before a purchase order', keywords: ['pr', 'request', 'requisition']),
  'purchasing-receipts': (section: 'Purchasing', subtitle: 'Goods received from suppliers', keywords: ['grn', 'goods received', 'receiving', 'delivery']),
  // HR
  'hr-employees': (section: 'HR', subtitle: 'Staff records and contracts', keywords: ['staff', 'people', 'employee']),
  'hr-payroll': (section: 'HR', subtitle: 'Monthly salary runs and payslips', keywords: ['salary', 'wages', 'payslip', 'wps']),
  'hr-reviews': (section: 'HR', subtitle: 'Employee performance reviews', keywords: ['appraisal', 'performance', 'review']),
  'hr-review-cycles': (section: 'HR', subtitle: 'Review periods and their deadlines', keywords: ['appraisal cycle', 'review period']),
  'hr-leave': (section: 'HR', subtitle: 'Request and approve time off', keywords: ['vacation', 'holiday', 'time off', 'annual leave', 'sick']),
  'hr-leave-balances': (section: 'HR', subtitle: 'Days of leave each person has left', keywords: ['leave balance', 'entitlement', 'days left']),
  'hr-attendance': (section: 'HR', subtitle: 'Check-ins, check-outs and absences', keywords: ['attendance', 'clock in', 'timesheet', 'absence']),
  'hr-leave-types': (section: 'HR', subtitle: 'Kinds of leave and their rules', keywords: ['leave policy', 'leave type']),
  'hr-holidays': (section: 'HR', subtitle: 'Public holidays on the calendar', keywords: ['holiday', 'calendar', 'eid', 'national day']),
  // CRM
  'crm-leads': (section: 'CRM', subtitle: 'Potential customers and their contact details', keywords: ['prospects', 'contacts', 'lead']),
  'crm-opportunities': (section: 'CRM', subtitle: 'Deals in progress, by stage', keywords: ['pipeline', 'opportunity', 'deals', 'funnel']),
  'crm-interactions': (section: 'CRM', subtitle: 'Calls, meetings and emails with contacts', keywords: ['activity', 'calls', 'meetings', 'log']),
  // Projects & services
  'projects-projects': (section: 'Projects', subtitle: 'Projects, budgets and progress', keywords: ['project', 'jobs']),
  'projects-tasks': (section: 'Projects', subtitle: 'To-dos across your projects', keywords: ['todo', 'task', 'assignments']),
  'projects-timesheets': (section: 'Projects', subtitle: 'Hours logged against projects', keywords: ['hours', 'time entries', 'timesheet']),
  'projects-billing': (section: 'Projects', subtitle: 'Project work that is ready to invoice', keywords: ['bill project', 'unbilled', 'wip']),
  'services-engagements': (section: 'Services', subtitle: 'Client engagements and their terms', keywords: ['engagement', 'contract', 'client']),
  'services-retainers': (section: 'Services', subtitle: 'Recurring fees billed to clients', keywords: ['retainer', 'recurring', 'subscription']),
  'services-milestones': (section: 'Services', subtitle: 'Bill clients when milestones are reached', keywords: ['milestone', 'stage billing']),
  // Real estate
  're-properties': (section: 'Real estate', subtitle: 'Buildings and land you manage', keywords: ['building', 'property', 'tower']),
  're-units': (section: 'Real estate', subtitle: 'Flats, shops and offices inside properties', keywords: ['unit', 'flat', 'apartment', 'shop', 'office']),
  're-unittypes': (section: 'Real estate', subtitle: 'Kinds of units, like 1BR or retail', keywords: ['unit type', '1br', 'studio']),
  're-leases': (section: 'Real estate', subtitle: 'Tenant contracts and their terms', keywords: ['lease', 'tenancy', 'contract', 'ejari', 'tenant']),
  're-billing': (section: 'Real estate', subtitle: 'Rent due and rent invoices', keywords: ['rent', 'rent invoice', 'billing']),
  're-renewals': (section: 'Real estate', subtitle: 'Leases coming up for renewal', keywords: ['renewal', 'expiring', 'renew']),
  're-workorders': (section: 'Real estate', subtitle: 'Repair and maintenance jobs at properties', keywords: ['maintenance', 'repair', 'work order', 'fix']),
  're-pm': (section: 'Real estate', subtitle: 'Scheduled maintenance that repeats', keywords: ['preventive', 'ppm', 'schedule']),
  're-inspections': (section: 'Real estate', subtitle: 'Move-in, move-out and routine inspections', keywords: ['inspection', 'check', 'move in', 'move out']),
  're-acquisitions': (section: 'Real estate', subtitle: 'Properties you are buying', keywords: ['acquisition', 'purchase', 'deal']),
  're-listings': (section: 'Real estate', subtitle: 'Units advertised for rent or sale', keywords: ['listing', 'advert', 'vacant', 'marketing']),
  're-leads': (section: 'Real estate', subtitle: 'People interested in renting or buying', keywords: ['enquiry', 'inquiry', 'prospect', 'lead']),
  're-deals': (section: 'Real estate', subtitle: 'Rent or sale deals in progress', keywords: ['deal', 'offer', 'negotiation']),
};

/// Screens already reachable as bottom tabs; the palette shows the tab instead to avoid duplicates.
const Set<String> tabScreenIds = {'overview', 'invoices', 'receipts', 'customers'};

/// Fixed entries: tabs, Settings, Switch workspace, Notifications, Appearance.
const List<PaletteEntry> corePaletteEntries = [
  PaletteEntry(id: 'tab.overview', label: 'Overview', subtitle: 'Your business at a glance: revenue, cash and alerts', icon: Icons.dashboard_outlined, keywords: ['home', 'dashboard', 'summary', 'kpi'], route: '/home', section: 'Tabs', data: TabTarget(HomeTabs.overview)),
  PaletteEntry(id: 'tab.invoices', label: 'Invoices', subtitle: 'Bills you sent to customers and what they still owe', icon: Icons.receipt_long_outlined, keywords: ['sales invoice', 'ar', 'billing', 'credit note'], route: '/home', section: 'Tabs', data: TabTarget(HomeTabs.invoices)),
  PaletteEntry(id: 'tab.receipts', label: 'Receipts', subtitle: 'Money customers paid you', icon: Icons.payments_outlined, keywords: ['collections', 'payment received', 'incoming'], route: '/home', section: 'Tabs', data: TabTarget(HomeTabs.receipts)),
  PaletteEntry(id: 'tab.customers', label: 'Customers', subtitle: 'People and companies you sell to, with balances', icon: Icons.people_outline, keywords: ['clients', 'buyers', 'parties', 'debtors'], route: '/home', section: 'Tabs', data: TabTarget(HomeTabs.customers)),
  PaletteEntry(id: 'tab.more', label: 'More', subtitle: 'Every other area of the app', icon: Icons.apps_outlined, keywords: ['modules', 'menu', 'all'], route: '/home', section: 'Tabs', data: TabTarget(HomeTabs.more)),
  PaletteEntry(id: 'settings', label: 'Settings', subtitle: 'Workspace, server and sign out', icon: Icons.settings_outlined, keywords: ['preferences', 'options', 'sign out', 'logout', 'server'], route: '/settings', section: 'Account', data: RouteTarget('/settings')),
  PaletteEntry(id: 'switch-workspace', label: 'Switch workspace', subtitle: 'Move to another company or branch', icon: Icons.swap_horiz_rounded, keywords: ['company', 'organisation', 'organization', 'tenant', 'entity', 'branch'], route: '/settings', section: 'Account', data: RouteTarget('/settings')),
  PaletteEntry(id: 'notifications', label: 'Notifications', subtitle: 'Alerts and things that need your attention', icon: Icons.notifications_none_rounded, keywords: ['alerts', 'inbox', 'bell', 'messages'], section: 'Account', data: ActionTarget(PaletteAction.notifications)),
  PaletteEntry(id: 'appearance', label: 'Appearance', subtitle: 'Light, dark or follow the system', icon: Icons.dark_mode_outlined, keywords: ['theme', 'dark mode', 'light mode', 'night'], section: 'Account', data: ActionTarget(PaletteAction.appearance)),
];

/// Every palette entry: [corePaletteEntries] then each screen the More grid can open.
///
/// Pass [allowedMenuIds] (ids from the server menu, `menuProvider`) to hide screens this user/workspace has no menu
/// entry for, the same rule the More grid follows. When null every screen is listed.
List<PaletteEntry> buildPaletteEntries({List<MobileScreen>? screens, Set<String>? allowedMenuIds}) {
  final out = <PaletteEntry>[...corePaletteEntries];
  for (final s in screens ?? allScreens) {
    if (tabScreenIds.contains(s.id)) continue;
    if (allowedMenuIds != null && !s.menuIds.any(allowedMenuIds.contains)) continue;
    final meta = _screenMeta[s.id];
    out.add(PaletteEntry(
      id: 'screen.${s.id}',
      label: s.title,
      subtitle: meta?.subtitle ?? 'Open ${s.title}',
      icon: s.icon,
      keywords: meta?.keywords ?? const <String>[],
      section: meta?.section,
      data: ScreenTarget(s),
    ));
  }
  return out;
}

/// Performs [entry]'s target. Call from `showCommandPalette(onSelect: ...)` with the caller's context.
///
/// [selectTab] switches the home shell's tab when the palette is opened from HomePage; elsewhere a tab entry goes to
/// '/home' (the shell opens on Overview).
void openPaletteEntry(BuildContext context, WidgetRef ref, PaletteEntry entry, {ValueChanged<int>? selectTab}) {
  final target = entry.data;
  switch (target) {
    case RouteTarget(:final path, :final push):
      push ? context.push(path) : context.go(path);
    case TabTarget(:final index):
      if (selectTab != null) {
        selectTab(index);
      } else {
        context.go('/home');
      }
    case ScreenTarget(:final screen):
      Navigator.of(context).push(MaterialPageRoute<void>(builder: screen.builder));
    case ActionTarget(action: PaletteAction.notifications):
      showNotificationsSheet(context);
    case ActionTarget(action: PaletteAction.appearance):
      // The Light/Dark/System switch lives in the account sheet.
      showProfileSheet(context, ref);
    case null:
      if (entry.route != null) context.push(entry.route!);
    default:
      break;
  }
}
