import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'approval_screens.dart';
import 'bill_screens.dart';
import 'finance_screens.dart';
import 'journal_screens.dart';
import 'payment_screens.dart';
import 'report_screens.dart';

/// Screens of this area for the More menu.
final List<MobileScreen> payablesScreens = <MobileScreen>[
  MobileScreen(id: 'bills', title: 'Bills', menuIds: const ['finance.bills'], icon: Icons.description_outlined, builder: (_) => const BillsScreen()),
  MobileScreen(id: 'payments', title: 'Payments', menuIds: const ['finance.payments'], icon: Icons.payments_outlined, builder: (_) => const PaymentsScreen()),
  MobileScreen(id: 'journals', title: 'Journals', menuIds: const ['finance.journals'], icon: Icons.menu_book_outlined, builder: (_) => const JournalsScreen()),
  MobileScreen(id: 'cheques', title: 'Cheque register', menuIds: const ['finance.cheques'], icon: Icons.credit_card_outlined, builder: (_) => const ChequesScreen()),
  MobileScreen(id: 'bank', title: 'Cash and bank', menuIds: const ['finance.bank'], icon: Icons.account_balance_outlined, builder: (_) => const BankScreen()),
  MobileScreen(id: 'budgets', title: 'Budgets', menuIds: const ['finance.budgets'], icon: Icons.bar_chart, builder: (_) => const BudgetsScreen()),
  MobileScreen(id: 'reports', title: 'Reports', menuIds: const ['core.reports'], icon: Icons.analytics_outlined, builder: (_) => const ReportsScreen()),
  MobileScreen(id: 'accounts', title: 'Chart of accounts', menuIds: const ['finance.accounts'], icon: Icons.account_tree_outlined, builder: (_) => const AccountsScreen()),
  MobileScreen(id: 'taxcodes', title: 'Tax codes', menuIds: const ['finance.taxcodes'], icon: Icons.percent, builder: (_) => const TaxCodesScreen()),
  MobileScreen(id: 'approvals', title: 'Approvals', menuIds: const ['core.approvals'], icon: Icons.fact_check_outlined, builder: (_) => const ApprovalsScreen()),
];
