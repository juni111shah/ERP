import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';

/// One thing the palette can open: a tab, a screen, a route or an action.
///
/// [route] is the go_router path when the destination has one (for example '/settings'); it is null for screens that
/// are pushed with Navigator (as the More grid does) and for actions such as opening a sheet. [data] is opaque to the
/// palette; the app keeps its own target there (see `core/palette_registry.dart`).
@immutable
class PaletteEntry {
  const PaletteEntry({
    required this.id,
    required this.label,
    required this.subtitle,
    required this.icon,
    this.keywords = const <String>[],
    this.route,
    this.section,
    this.data,
  });

  /// Stable id; recents are stored by id.
  final String id;
  final String label;

  /// One plain-language line saying what you can do there.
  final String subtitle;
  final IconData icon;

  /// Extra words people might type (synonyms, Arabic transliterations, codes).
  final List<String> keywords;
  final String? route;

  /// Area name shown as a small caption (Finance, Sales...).
  final String? section;
  final Object? data;

  @override
  String toString() => 'PaletteEntry($id)';
}

/// Most-recent-first list of entry ids. Inject your own (e.g. persisted) implementation into [showCommandPalette].
abstract class PaletteRecents implements Listenable {
  List<String> get ids;
  void add(String id);
  void clear();
}

/// In-memory recents, newest first, capped at [capacity]. Lives as long as the object (keep one app-wide instance).
class MemoryPaletteRecents extends ChangeNotifier implements PaletteRecents {
  MemoryPaletteRecents({this.capacity = 5, Iterable<String> initial = const <String>[]})
      : _ids = initial.take(capacity).toList();

  final int capacity;
  final List<String> _ids;

  @override
  List<String> get ids => List.unmodifiable(_ids);

  @override
  void add(String id) {
    _ids
      ..remove(id)
      ..insert(0, id);
    if (_ids.length > capacity) _ids.removeRange(capacity, _ids.length);
    notifyListeners();
  }

  @override
  void clear() {
    _ids.clear();
    notifyListeners();
  }
}

/// App-wide default recents used when none is passed.
final PaletteRecents defaultPaletteRecents = MemoryPaletteRecents();

/// Result of matching one entry against a query.
@immutable
class PaletteMatch {
  const PaletteMatch(this.entry, this.score, this.labelHits);

  final PaletteEntry entry;
  final double score;

  /// Indices in [PaletteEntry.label] that matched (for highlighting); empty when the label did not match.
  final List<int> labelHits;
}

/// Subsequence score of [query] in [text] (both lowercased by the caller), or null if [query] is not a subsequence.
/// Rewards consecutive runs, word starts and a match at the very start; penalises gaps.
({double score, List<int> hits})? fuzzyScore(String query, String text) {
  if (query.isEmpty) return (score: 0, hits: const <int>[]);
  final hits = <int>[];
  var score = 0.0;
  var ti = 0;
  var prev = -2;
  for (var qi = 0; qi < query.length; qi++) {
    final c = query[qi];
    if (c == ' ') continue;
    // Keep a consecutive run if possible, else jump to the next word start, else the next occurrence.
    var found = -1;
    if (ti < text.length && text[ti] == c && prev == ti - 1 && prev >= 0) {
      found = ti;
    } else {
      for (var j = ti; j < text.length; j++) {
        if (text[j] == c && (j == 0 || !_isAlnum(text[j - 1]))) {
          found = j;
          break;
        }
      }
      if (found < 0) found = text.indexOf(c, ti);
    }
    if (found < 0) return null;
    var s = 1.0;
    if (found == prev + 1) s += 2; // consecutive
    if (found == 0 || !_isAlnum(text[found - 1])) s += 3; // word start
    if (found == 0) s += 2; // very start
    s -= (found - ti) * 0.05; // gap
    score += s;
    hits.add(found);
    prev = found;
    ti = found + 1;
  }
  // Shorter texts win ties; an exact match wins outright.
  score += 4 / (1 + text.length);
  if (text == query.trim()) score += 10;
  return (score: score, hits: hits);
}

bool _isAlnum(String ch) {
  final u = ch.codeUnitAt(0);
  return (u >= 48 && u <= 57) || (u >= 97 && u <= 122) || u > 127;
}

/// Score of [entry] for [query]: the best of label (weight 1), keywords (0.75) and subtitle (0.4); null when none match.
PaletteMatch? scorePaletteEntry(String query, PaletteEntry entry) {
  final q = query.trim().toLowerCase();
  if (q.isEmpty) return PaletteMatch(entry, 0, const <int>[]);
  final label = fuzzyScore(q, entry.label.toLowerCase());
  var best = label == null ? double.negativeInfinity : label.score;
  for (final k in entry.keywords) {
    final m = fuzzyScore(q, k.toLowerCase());
    if (m != null && m.score * 0.75 > best) best = m.score * 0.75;
  }
  final sub = fuzzyScore(q, entry.subtitle.toLowerCase());
  if (sub != null && sub.score * 0.4 > best) best = sub.score * 0.4;
  if (best == double.negativeInfinity) return null;
  return PaletteMatch(entry, best, label?.hits ?? const <int>[]);
}

/// Entries matching [query], best first (stable for equal scores). An empty query returns every entry, unscored.
List<PaletteMatch> rankPaletteEntries(String query, List<PaletteEntry> entries) {
  final out = <(int, PaletteMatch)>[];
  for (var i = 0; i < entries.length; i++) {
    final m = scorePaletteEntry(query, entries[i]);
    if (m != null) out.add((i, m));
  }
  if (query.trim().isNotEmpty) {
    out.sort((a, b) {
      final c = b.$2.score.compareTo(a.$2.score);
      return c != 0 ? c : a.$1.compareTo(b.$1);
    });
  }
  return [for (final o in out) o.$2];
}

/// Opens the palette as a tall bottom sheet. Resolves to the chosen entry (null if dismissed); [onSelect] runs after
/// the sheet has closed, with the caller's context still valid, so it can navigate (context.go / push).
Future<PaletteEntry?> showCommandPalette(
  BuildContext context, {
  required List<PaletteEntry> entries,
  PaletteRecents? recents,
  ValueChanged<PaletteEntry>? onSelect,
  String hintText = 'Search screens and actions',
}) async {
  final store = recents ?? defaultPaletteRecents;
  final picked = await showModalBottomSheet<PaletteEntry>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    backgroundColor: ErpTheme.of(context).surface,
    shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(ErpRadius.panel))),
    builder: (ctx) => FractionallySizedBox(
      heightFactor: 0.9,
      child: CommandPalette(
        entries: entries,
        recents: store,
        hintText: hintText,
        onSelect: (e) => Navigator.of(ctx).pop(e),
      ),
    ),
  );
  if (picked != null) {
    store.add(picked.id);
    onSelect?.call(picked);
  }
  return picked;
}

/// The palette body: a search field over a ranked list. Embeddable (tests, tablets, a full page).
///
/// Keyboard: ↑/↓ move the highlight, Enter opens it, Esc closes (pops the route). With an empty query, recents come
/// first under "Recent", then everything under "All".
class CommandPalette extends StatefulWidget {
  const CommandPalette({
    super.key,
    required this.entries,
    required this.onSelect,
    this.recents,
    this.hintText = 'Search screens and actions',
    this.autofocus = true,
    this.onClose,
  });

  final List<PaletteEntry> entries;
  final ValueChanged<PaletteEntry> onSelect;
  final PaletteRecents? recents;
  final String hintText;
  final bool autofocus;

  /// Esc handler; defaults to `Navigator.maybePop`.
  final VoidCallback? onClose;

  @override
  State<CommandPalette> createState() => _CommandPaletteState();
}

sealed class _Row {
  const _Row();
}

class _Header extends _Row {
  const _Header(this.text);
  final String text;
}

class _Hit extends _Row {
  const _Hit(this.match);
  final PaletteMatch match;
}

class _CommandPaletteState extends State<CommandPalette> {
  final _controller = TextEditingController();
  final _focus = FocusNode(debugLabel: 'palette-search');
  final _highlightKey = GlobalKey();
  List<_Row> _rows = const [];
  List<PaletteMatch> _selectable = const [];
  int _highlight = 0;
  bool _done = false;

  @override
  void initState() {
    super.initState();
    _controller.addListener(_rebuild);
    widget.recents?.addListener(_rebuild);
    _compute();
  }

  @override
  void didUpdateWidget(CommandPalette old) {
    super.didUpdateWidget(old);
    if (old.recents != widget.recents) {
      old.recents?.removeListener(_rebuild);
      widget.recents?.addListener(_rebuild);
    }
    if (old.entries != widget.entries || old.recents != widget.recents) _rebuild();
  }

  @override
  void dispose() {
    widget.recents?.removeListener(_rebuild);
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _rebuild() => setState(_compute);

  void _compute() {
    final q = _controller.text;
    final rows = <_Row>[];
    if (q.trim().isEmpty) {
      final byId = {for (final e in widget.entries) e.id: e};
      final recent = [for (final id in widget.recents?.ids ?? const <String>[]) if (byId[id] != null) byId[id]!];
      if (recent.isNotEmpty) {
        rows.add(const _Header('Recent'));
        rows.addAll(recent.map((e) => _Hit(PaletteMatch(e, 0, const []))));
        rows.add(const _Header('All'));
      }
      final recentIds = recent.map((e) => e.id).toSet();
      rows.addAll(widget.entries.where((e) => !recentIds.contains(e.id)).map((e) => _Hit(PaletteMatch(e, 0, const []))));
    } else {
      rows.addAll(rankPaletteEntries(q, widget.entries).map(_Hit.new));
    }
    _rows = rows;
    _selectable = [for (final r in rows) if (r is _Hit) r.match];
    _highlight = 0;
  }

  void _move(int delta) {
    if (_selectable.isEmpty) return;
    setState(() => _highlight = (_highlight + delta).clamp(0, _selectable.length - 1));
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final ctx = _highlightKey.currentContext;
      if (ctx != null) Scrollable.ensureVisible(ctx, duration: const Duration(milliseconds: 120), alignmentPolicy: delta > 0 ? ScrollPositionAlignmentPolicy.keepVisibleAtEnd : ScrollPositionAlignmentPolicy.keepVisibleAtStart);
    });
  }

  void _choose(PaletteEntry e) {
    if (_done) return;
    _done = true;
    widget.onSelect(e);
  }

  void _submit() {
    if (_selectable.isNotEmpty) _choose(_selectable[_highlight].entry);
  }

  void _close() {
    if (widget.onClose != null) {
      widget.onClose!();
    } else {
      Navigator.of(context).maybePop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final theme = Theme.of(context);
    final query = _controller.text.trim();
    return CallbackShortcuts(
      bindings: <ShortcutActivator, VoidCallback>{
        const SingleActivator(LogicalKeyboardKey.arrowDown): () => _move(1),
        const SingleActivator(LogicalKeyboardKey.arrowUp): () => _move(-1),
        const SingleActivator(LogicalKeyboardKey.enter): _submit,
        const SingleActivator(LogicalKeyboardKey.numpadEnter): _submit,
        const SingleActivator(LogicalKeyboardKey.escape): _close,
      },
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s2),
            child: TextField(
              key: const ValueKey('palette-search'),
              controller: _controller,
              focusNode: _focus,
              autofocus: widget.autofocus,
              textInputAction: TextInputAction.go,
              onSubmitted: (_) => _submit(),
              decoration: InputDecoration(
                hintText: widget.hintText,
                prefixIcon: const Icon(Icons.search_rounded),
                suffixIcon: query.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'Clear search',
                        icon: const Icon(Icons.close_rounded),
                        onPressed: () {
                          _controller.clear();
                          _focus.requestFocus();
                        },
                      ),
              ),
            ),
          ),
          Expanded(
            child: _selectable.isEmpty
                ? _EmptyState(query: query)
                : Semantics(
                    container: true,
                    label: query.isEmpty ? 'All destinations' : '${_selectable.length} results',
                    child: ListView.builder(
                      key: const ValueKey('palette-list'),
                      padding: EdgeInsetsDirectional.only(bottom: MediaQuery.paddingOf(context).bottom + ErpSpace.s4),
                      itemCount: _rows.length,
                      itemBuilder: (context, i) {
                        final row = _rows[i];
                        return switch (row) {
                          _Header(:final text) => Padding(
                              padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s4, ErpSpace.s1),
                              child: Semantics(
                                header: true,
                                child: Text(text.toUpperCase(),
                                    style: theme.textTheme.labelSmall?.copyWith(color: t.muted, letterSpacing: 0.6, fontWeight: FontWeight.w600)),
                              ),
                            ),
                          _Hit(:final match) => () {
                              final selected = identical(match, _selectable[_highlight]);
                              return _PaletteRow(
                                key: selected ? _highlightKey : ValueKey('palette-row-${match.entry.id}'),
                                match: match,
                                highlighted: selected,
                                onTap: () => _choose(match.entry),
                              );
                            }(),
                        };
                      },
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

class _PaletteRow extends StatelessWidget {
  const _PaletteRow({super.key, required this.match, required this.highlighted, required this.onTap});

  final PaletteMatch match;
  final bool highlighted;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final textTheme = Theme.of(context).textTheme;
    final e = match.entry;
    final labelStyle = textTheme.bodyLarge?.copyWith(color: t.text, fontWeight: FontWeight.w500) ?? TextStyle(color: t.text);
    final hits = match.labelHits.toSet();
    final label = hits.isEmpty
        ? TextSpan(text: e.label, style: labelStyle)
        : TextSpan(children: [
            for (var i = 0; i < e.label.length; i++)
              TextSpan(
                text: e.label[i],
                style: hits.contains(i) ? labelStyle.copyWith(color: t.brand, fontWeight: FontWeight.w700) : labelStyle,
              ),
          ]);
    return Semantics(
      button: true,
      selected: highlighted,
      label: '${e.label}. ${e.subtitle}',
      excludeSemantics: true,
      onTap: onTap,
      child: Material(
        color: highlighted ? t.brandSubtle : Colors.transparent,
        child: InkWell(
          onTap: onTap,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 56),
            child: Padding(
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s4, vertical: ErpSpace.s2),
              child: Row(
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    alignment: AlignmentDirectional.center,
                    decoration: BoxDecoration(color: t.sunken, borderRadius: BorderRadius.circular(ErpRadius.input)),
                    child: Icon(e.icon, size: 20, color: highlighted ? t.brand : t.text2),
                  ),
                  const SizedBox(width: ErpSpace.s3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text.rich(label, maxLines: 1, overflow: TextOverflow.ellipsis),
                        const SizedBox(height: 2),
                        Text(
                          e.subtitle,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: textTheme.bodySmall?.copyWith(color: t.muted),
                        ),
                      ],
                    ),
                  ),
                  if (e.section != null) ...[
                    const SizedBox(width: ErpSpace.s2),
                    Text(e.section!, style: textTheme.labelSmall?.copyWith(color: t.muted)),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.query});

  final String query;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final textTheme = Theme.of(context).textTheme;
    return Semantics(
      liveRegion: true,
      child: Padding(
        padding: const EdgeInsetsDirectional.all(ErpSpace.s6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.search_off_rounded, size: 40, color: t.muted),
            const SizedBox(height: ErpSpace.s3),
            Text(
              query.isEmpty ? 'Nothing to show yet' : 'No matches for “$query”',
              textAlign: TextAlign.center,
              style: textTheme.titleSmall?.copyWith(color: t.text),
            ),
            const SizedBox(height: ErpSpace.s1),
            Text(
              'Try fewer letters, like “inv” for invoices.',
              textAlign: TextAlign.center,
              style: textTheme.bodySmall?.copyWith(color: t.muted),
            ),
          ],
        ),
      ),
    );
  }
}
