import 'package:flutter/material.dart';

/// One screen of the app that corresponds to one or more entries of the server menu.
///
/// [menuIds] are the ids the server menu uses (for example 'finance.invoices', 'sales.orders'; core ones are in
/// `backend/src/ERP.Modularity/CoreMenu.cs`, module ones in each `backend/modules/*/…Module.cs`). The More screen shows the menu the
/// server returns for the user and opens the matching [MobileScreen] where there is one.
class MobileScreen {
  const MobileScreen({required this.id, required this.title, required this.menuIds, required this.builder, this.icon = Icons.article_outlined});

  final String id;
  final String title;
  final List<String> menuIds;
  final IconData icon;

  /// The screen itself; it is pushed on the navigator, so it should be a Scaffold (a [ListScreen] is).
  final WidgetBuilder builder;
}
