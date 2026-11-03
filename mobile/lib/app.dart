import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'core/brand.dart';
import 'core/session.dart';
import 'core/theme_mode.dart';
import 'features/auth/login_page.dart';
import 'features/auth/workspace_page.dart';
import 'features/home/home_page.dart';
import 'features/intro/intro_flag.dart';
import 'features/intro/intro_page.dart';
import 'features/settings/settings_page.dart';
import 'kit/brand_mark.dart';
import 'theme/app_theme.dart';

/// Routes follow the session: signed out goes to sign-in (or workspace choice), signed in goes to the home shell.
/// On the very first open the intro tour ([IntroKeys.route]) comes first, until it is skipped or finished.
final routerProvider = Provider<GoRouter>((ref) {
  final changes = ValueNotifier<int>(0);
  ref.listen(sessionProvider, (_, _) => changes.value++);
  ref.listen(pendingWorkspaceProvider, (_, _) => changes.value++);
  ref.listen(introProvider, (_, _) => changes.value++);
  ref.onDispose(changes.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: changes,
    redirect: (context, state) {
      final at = state.matchedLocation;
      final intro = ref.read(introProvider);
      if (intro == IntroStatus.unknown) return at == '/splash' ? null : '/splash';
      if (intro == IntroStatus.show) return at == IntroKeys.route ? null : IntroKeys.route;
      final status = ref.read(sessionProvider).status;
      if (status == AuthStatus.loading) return at == '/splash' ? null : '/splash';
      if (status == AuthStatus.signedOut) {
        final choosing = ref.read(pendingWorkspaceProvider) != null;
        final target = choosing ? '/workspaces' : '/login';
        return at == target ? null : target;
      }
      return const {'/splash', '/login', '/workspaces', IntroKeys.route}.contains(at) ? '/home' : null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const BrandSplash()),
      GoRoute(path: IntroKeys.route, builder: (_, _) => const IntroPage()),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(path: '/workspaces', builder: (_, _) => const WorkspacePage()),
      GoRoute(path: '/home', builder: (_, _) => const HomePage()),
      GoRoute(path: '/settings', builder: (_, _) => const SettingsPage()),
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
    Future.microtask(() {
      ref.read(introProvider.notifier).load();
      ref.read(sessionProvider.notifier).bootstrap();
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: Brand.name,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ref.watch(themeModeProvider),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
