import 'package:flutter/foundation.dart' show mapEquals;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../common/paged_list_view.dart' show ErrorView;

/// Loads [path] (with [query]) and builds [builder] from the answer; reloads when the query changes or after any save.
class ApiView extends ConsumerStatefulWidget {
  const ApiView({super.key, required this.path, required this.builder, this.query = const {}});

  final String path;
  final Map<String, dynamic> query;
  final Widget Function(BuildContext context, dynamic data) builder;

  @override
  ConsumerState<ApiView> createState() => _ApiViewState();
}

class _ApiViewState extends ConsumerState<ApiView> {
  dynamic _data;
  String? _error;
  bool _loading = true;
  int _gen = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant ApiView old) {
    super.didUpdateWidget(old);
    if (old.path != widget.path || !mapEquals(old.query, widget.query)) _load();
  }

  Future<void> _load() async {
    final gen = ++_gen;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await ref.read(apiProvider).get(widget.path, query: widget.query);
      if (mounted && gen == _gen) setState(() => _data = data);
    } on ApiException catch (e) {
      if (mounted && gen == _gen) setState(() => _error = e.message);
    } finally {
      if (mounted && gen == _gen) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    if (_loading && _data == null) return const Center(child: CircularProgressIndicator());
    if (_error != null) return ErrorView(message: _error!, onRetry: _load);
    return RefreshIndicator(onRefresh: _load, child: widget.builder(context, _data));
  }
}
