import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';
import 'core/key_value_store.dart';
import 'core/session.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final store = SecureKeyValueStore();
  final saved = await store.read(StoreKeys.serverUrl);
  runApp(ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      initialServerUrlProvider.overrideWithValue(saved ?? defaultServerUrl()),
    ],
    child: const ErpApp(),
  ));
}
