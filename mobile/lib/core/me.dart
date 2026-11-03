import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'session.dart';

class LegalEntity {
  const LegalEntity({required this.id, required this.code, required this.name, required this.baseCurrency});

  final String id;
  final String code;
  final String name;
  final String baseCurrency;

  factory LegalEntity.fromJson(Map<String, dynamic> json) => LegalEntity(
        id: json['id'].toString(),
        code: (json['code'] ?? '').toString(),
        name: (json['name'] ?? '').toString(),
        baseCurrency: (json['baseCurrency'] ?? '').toString(),
      );
}

/// What the app needs to know after sign-in (`GET /api/v1/me`): who, what the user may do, which legal entities and which modules are on.
class Me {
  const Me({
    required this.fullName,
    required this.email,
    required this.baseCurrency,
    required this.countryCode,
    required this.permissions,
    required this.legalEntities,
    required this.modules,
  });

  final String fullName;
  final String email;
  final String baseCurrency;
  final String countryCode;
  final Set<String> permissions;
  final List<LegalEntity> legalEntities;

  /// Keys of the enabled modules, for example "sales", "pos".
  final Set<String> modules;

  /// The API expands "*" for owners; a user holding it may do everything.
  bool can(String? permission) => permission == null || permissions.contains('*') || permissions.contains(permission);

  bool hasModule(String key) => modules.contains(key);

  /// The legal entity to pre-select on new documents: the only one, or the first.
  LegalEntity? get defaultEntity => legalEntities.isEmpty ? null : legalEntities.first;

  factory Me.fromJson(Map<String, dynamic> json) {
    final user = (json['user'] as Map<String, dynamic>?) ?? const {};
    final workspace = (json['workspace'] as Map<String, dynamic>?) ?? const {};
    return Me(
      fullName: (user['fullName'] ?? '').toString(),
      email: (user['email'] ?? '').toString(),
      baseCurrency: (workspace['baseCurrency'] ?? '').toString(),
      countryCode: (workspace['countryCode'] ?? '').toString(),
      permissions: {for (final p in (json['permissions'] as List<dynamic>? ?? const [])) p.toString()},
      legalEntities: [for (final e in (json['legalEntities'] as List<dynamic>? ?? const [])) LegalEntity.fromJson(e as Map<String, dynamic>)],
      modules: {for (final m in (json['enabledModules'] as List<dynamic>? ?? const [])) (m as Map<String, dynamic>)['key'].toString()},
    );
  }

  static const empty = Me(fullName: '', email: '', baseCurrency: '', countryCode: '', permissions: {}, legalEntities: [], modules: {});
}

/// One entry of the server-driven menu. [route] is the web route; the app maps it to one of its own screens when it has one.
class MenuItem {
  const MenuItem({required this.id, required this.label, required this.route, required this.module, this.icon, this.permission});

  final String id;
  final String label;
  final String route;
  final String module;
  final String? icon;
  final String? permission;

  factory MenuItem.fromJson(Map<String, dynamic> json) => MenuItem(
        id: json['id'].toString(),
        label: (json['label'] ?? '').toString(),
        route: (json['route'] ?? '').toString(),
        module: (json['module'] ?? '').toString(),
        icon: json['icon'] as String?,
        permission: json['permission'] as String?,
      );
}

class MenuGroup {
  const MenuGroup({required this.id, required this.label, required this.items});

  final String id;
  final String? label;
  final List<MenuItem> items;

  factory MenuGroup.fromJson(Map<String, dynamic> json) => MenuGroup(
        id: json['id'].toString(),
        label: json['label'] as String?,
        items: [for (final i in (json['items'] as List<dynamic>? ?? const [])) MenuItem.fromJson(i as Map<String, dynamic>)],
      );
}

/// Reloads when the workspace changes.
final meProvider = FutureProvider<Me>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  final data = await ref.watch(apiProvider).get('/api/v1/me');
  return Me.fromJson(data as Map<String, dynamic>);
});

final menuProvider = FutureProvider<List<MenuGroup>>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  final data = await ref.watch(apiProvider).get('/api/v1/navigation/menu');
  return [for (final g in (data as List<dynamic>)) MenuGroup.fromJson(g as Map<String, dynamic>)];
});

/// `ref.watch(meOrEmptyProvider)`: the current [Me], or an empty one while loading so permission checks simply say no.
final meOrEmptyProvider = Provider<Me>((ref) => ref.watch(meProvider).asData?.value ?? Me.empty);
