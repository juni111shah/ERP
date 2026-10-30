import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Bumped after every successful write (form saved, action run). Lists and detail screens listen to it and reload.
class DataVersion extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final dataVersionProvider = NotifierProvider<DataVersion, int>(DataVersion.new);
