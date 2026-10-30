import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'core/session.dart';
import 'features/auth/login_page.dart';
import 'features/auth/workspace_page.dart';
import 'features/documents/documents_pages.dart';
import 'features/home/home_page.dart';
import 'features/settings/settings_page.dart';

/// Routes follow the session: signed out goes to sign-in (or workspace choice), signed in goes to the home shell.
final routerProvider = Provider<GoRouter>((ref) {
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionProvider, (_, _) => changes.value++);
  ref.listen(pendingWorkspaceProvider, (_, _) => changes.value++);
  ref.onDispose(changes.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: changes,
    redirect: (context, state) {
      final status = ref.read(sessionProvider).status;
      final at = state.matchedLocation;
      if (status == AuthStatus.loading) return at == '/splash' ? null : '/splash';
      if (status == AuthStatus.signedOut) {
        final choosing = ref.read(pendingWorkspaceProvider) != null;
        final target = choosing ? '/workspaces' : '/login';
        return at == target ? null : target;
      }
      return const {'/splash', '/login', '/workspaces'}.contains(at) ? '/home' : null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const Scaffold(body: Center(child: CircularProgressIndicator()))),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(path: '/workspaces', builder: (_, _) => const WorkspacePage()),
      GoRoute(path: '/home', builder: (_, _) => const HomePage()),
      GoRoute(path: '/settings', builder: (_, _) => const SettingsPage()),
      GoRoute(path: '/invoices/:id', builder: (_, state) => InvoiceDetailPage(id: state.pathParameters['id']!)),
    ],
  );
});

class ErpApp extends ConsumerStatefulWidget {
  const ErpApp({super.key});

  @override
  ConsumerState<ErpApp> createState() => _ErpAppState();
}

class _ErpAppState extends ConsumerState<ErpApp> {
  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(sessionProvider.notifier).bootstrap());
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'ERP',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(colorSchemeSeed: const Color(0xFF1565C0), useMaterial3: true),
      darkTheme: ThemeData(colorSchemeSeed: const Color(0xFF1565C0), brightness: Brightness.dark, useMaterial3: true),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
