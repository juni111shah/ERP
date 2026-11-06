import 'package:flutter/painting.dart' show TextDirection;

/// One run of a reply: plain words, bold words, or words linking to a page on this site.
class AssistantInline {
  const AssistantInline(this.text, {this.bold = false, this.link});

  final String text;
  final bool bold;
  final String? link;
}

sealed class AssistantBlock {
  const AssistantBlock();
}

class AssistantParagraph extends AssistantBlock {
  const AssistantParagraph(this.parts);
  final List<AssistantInline> parts;
}

class AssistantList extends AssistantBlock {
  AssistantList({required this.ordered, required this.items});
  final bool ordered;
  final List<List<AssistantInline>> items;
}

final _inline = RegExp(r'\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)');
final _bullet = RegExp(r'^\s*[-*•]\s+(.*)$');
final _numbered = RegExp(r'^\s*\d+[.)]\s+(.*)$');
final _tableRow = RegExp(r'^\s*\|(.*)\|\s*$');
final _tableRule = RegExp(r'^\s*\|[\s:|-]*-[\s:|-]*\|\s*$');
final _heading = RegExp(r'^#{1,6}\s+');

/// Only an address on this site. A protocol-relative or backslash path could lead elsewhere.
bool isInternalLink(String href) => RegExp(r'^/(?![/\\])').hasMatch(href) && !href.contains(r'\');

List<AssistantInline> _parts(String line) {
  final parts = <AssistantInline>[];
  var at = 0;
  for (final m in _inline.allMatches(line)) {
    if (m.start > at) parts.add(AssistantInline(line.substring(at, m.start)));
    if (m.group(1) != null) {
      parts.add(AssistantInline(m.group(1)!, bold: true));
    } else {
      // A link to anywhere else keeps its words and loses its address, so a reply can never send someone off the site.
      parts.add(AssistantInline(m.group(2)!, link: isInternalLink(m.group(3)!) ? m.group(3) : null));
    }
    at = m.end;
  }
  if (at < line.length) parts.add(AssistantInline(line.substring(at)));
  return parts;
}

/// Turns a model's reply into a small set of blocks for widgets to draw as text (same rules as the web's `assistant-text.ts`). Nothing that
/// looks like a tag or an image is treated as anything but words. A table's rows become list items and its heading row is dropped.
List<AssistantBlock> parseAssistantText(String text) {
  final blocks = <AssistantBlock>[];
  final lines = text.split('\n');
  void addItem(bool ordered, List<AssistantInline> item) {
    final last = blocks.isEmpty ? null : blocks.last;
    if (last is AssistantList && last.ordered == ordered) {
      last.items.add(item);
    } else {
      blocks.add(AssistantList(ordered: ordered, items: [item]));
    }
  }

  for (var at = 0; at < lines.length; at++) {
    final line = lines[at];
    if (_tableRule.hasMatch(line) || (at + 1 < lines.length && _tableRule.hasMatch(lines[at + 1]))) continue;
    final row = _tableRow.firstMatch(line);
    final bullet = _bullet.firstMatch(line);
    final numbered = bullet == null ? _numbered.firstMatch(line) : null;
    if (row != null) {
      addItem(false, _parts(row.group(1)!.split('|').map((cell) => cell.trim()).where((cell) => cell.isNotEmpty).join(' · ')));
    } else if (bullet != null || numbered != null) {
      addItem(numbered != null, _parts((bullet ?? numbered)!.group(1)!));
    } else if (line.trim().isNotEmpty) {
      blocks.add(AssistantParagraph(_parts(line.trim().replaceFirst(_heading, ''))));
    }
  }
  return blocks;
}

final _firstLetter = RegExp(r'\p{L}', unicode: true);
final _rtlLetter = RegExp('[֐-ࣿיִ-﷿ﹰ-﻿]');

/// The direction of [text] from its first letter, as the web's `dir="auto"` does; null when it has no letters, so the caller keeps its own.
TextDirection? assistantDirection(String text) {
  final letter = _firstLetter.firstMatch(text)?.group(0);
  if (letter == null) return null;
  return _rtlLetter.hasMatch(letter) ? TextDirection.rtl : TextDirection.ltr;
}
