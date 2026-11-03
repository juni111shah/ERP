import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// A search icon button that expands (300ms, easeInOut) into a full-width
/// field with a brand focus ring and a clear button (web ExpandSearch;
/// mockup `.xsearch`: 40dp high, 10dp radius, 1.5dp brand border, 3dp
/// brand ring at 12%).
///
/// Place it where it can take the remaining width, e.g. inside an
/// [Expanded] in an app-bar row. Collapsed it is a 48x48 icon button aligned
/// to the end edge; expanded it fills the width. Mirrors in RTL.
///
/// All strings are parameters: [hintText], [openTooltip], [clearTooltip],
/// [closeTooltip].
class ExpandSearch extends StatefulWidget {
  const ExpandSearch({
    super.key,
    required this.hintText,
    required this.openTooltip,
    required this.clearTooltip,
    this.closeTooltip,
    this.onChanged,
    this.onSubmitted,
    this.onClosed,
    this.onOpened,
    this.controller,
    this.initiallyExpanded = false,
    this.showCloseButton = false,
    this.duration = const Duration(milliseconds: 300),
  });

  final String hintText;

  /// Tooltip / semantics of the collapsed search icon.
  final String openTooltip;

  /// Tooltip / semantics of the clear (x) button.
  final String clearTooltip;

  /// Tooltip / semantics of the optional leading back/close button.
  final String? closeTooltip;

  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;

  /// Called after the field collapses (via close button, or clear on an
  /// empty field, or the system back gesture while open).
  final VoidCallback? onClosed;
  final VoidCallback? onOpened;

  final TextEditingController? controller;
  final bool initiallyExpanded;

  /// Show a leading back arrow inside the expanded state (mockup screen 6).
  final bool showCloseButton;

  final Duration duration;

  @override
  State<ExpandSearch> createState() => _ExpandSearchState();
}

class _ExpandSearchState extends State<ExpandSearch> {
  late final TextEditingController _controller = widget.controller ?? TextEditingController();
  final FocusNode _focus = FocusNode();
  late bool _expanded = widget.initiallyExpanded;

  @override
  void initState() {
    super.initState();
    _focus.addListener(_onFocus);
    _controller.addListener(_onText);
  }

  @override
  void dispose() {
    _focus.removeListener(_onFocus);
    _controller.removeListener(_onText);
    _focus.dispose();
    if (widget.controller == null) _controller.dispose();
    super.dispose();
  }

  void _onFocus() => setState(() {});
  void _onText() => setState(() {});

  void _open() {
    setState(() => _expanded = true);
    widget.onOpened?.call();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _focus.requestFocus();
    });
  }

  void _close() {
    if (_controller.text.isNotEmpty) {
      _controller.clear();
      widget.onChanged?.call('');
    }
    _focus.unfocus();
    setState(() => _expanded = false);
    widget.onClosed?.call();
  }

  void _clear() {
    if (_controller.text.isEmpty) {
      _close();
      return;
    }
    _controller.clear();
    widget.onChanged?.call('');
    _focus.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    final focused = _focus.hasFocus;
    final field = AnimatedContainer(
      duration: widget.duration,
      curve: Curves.easeInOut,
      height: 40,
      decoration: BoxDecoration(
        color: ErpColors.surface,
        borderRadius: BorderRadius.circular(ErpRadius.input),
        border: Border.all(
          color: focused ? ErpColors.brand : ErpColors.border,
          width: focused ? 1.5 : 1,
        ),
        boxShadow: [
          BoxShadow(
            color: ErpColors.brand.withValues(alpha: focused ? 0.12 : 0),
            spreadRadius: 3,
          ),
        ],
      ),
      padding: const EdgeInsetsDirectional.only(start: ErpSpace.s3),
      child: Row(
        children: [
          const Icon(Icons.search, size: 18, color: ErpColors.muted),
          const SizedBox(width: ErpSpace.s2),
          Expanded(
            child: TextField(
              controller: _controller,
              focusNode: _focus,
              onChanged: widget.onChanged,
              onSubmitted: widget.onSubmitted,
              textInputAction: TextInputAction.search,
              cursorColor: ErpColors.brand,
              style: const TextStyle(fontSize: 14, color: ErpColors.text),
              decoration: InputDecoration(
                isCollapsed: true,
                border: InputBorder.none,
                hintText: widget.hintText,
                hintStyle: const TextStyle(fontSize: 14, color: ErpColors.muted),
              ),
            ),
          ),
          IconButton(
            onPressed: _clear,
            tooltip: widget.clearTooltip,
            iconSize: 18,
            color: ErpColors.muted,
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(minWidth: 40, minHeight: 40),
            icon: const Icon(Icons.close),
          ),
        ],
      ),
    );

    final expandedRow = Row(
      children: [
        if (widget.showCloseButton)
          IconButton(
            onPressed: _close,
            tooltip: widget.closeTooltip,
            icon: const BackButtonIcon(),
          ),
        Expanded(child: field),
      ],
    );

    final collapsed = Align(
      alignment: AlignmentDirectional.centerEnd,
      child: IconButton(
        onPressed: _open,
        tooltip: widget.openTooltip,
        icon: const Icon(Icons.search),
      ),
    );

    return PopScope(
      canPop: !_expanded,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop && _expanded) _close();
      },
      child: AnimatedSize(
        duration: widget.duration,
        curve: Curves.easeInOut,
        alignment: AlignmentDirectional.centerEnd,
        child: SizedBox(
          height: 48,
          child: AnimatedSwitcher(
            duration: widget.duration,
            switchInCurve: Curves.easeInOut,
            switchOutCurve: Curves.easeInOut,
            transitionBuilder: (child, anim) => FadeTransition(
              opacity: anim,
              child: SizeTransition(
                sizeFactor: anim,
                axis: Axis.horizontal,
                alignment: AlignmentDirectional.centerEnd,
                child: child,
              ),
            ),
            child: _expanded
                ? KeyedSubtree(key: const ValueKey('open'), child: expandedRow)
                : KeyedSubtree(key: const ValueKey('closed'), child: collapsed),
          ),
        ),
      ),
    );
  }
}
