/** A run of reply text: plain, bold, or a link to a page of this site. */
export type AssistantInline = { text: string; bold?: boolean; link?: string };
/** A paragraph or a list of a reply, as the chat panel renders it. */
export type AssistantBlock = { kind: 'paragraph'; parts: AssistantInline[] } | { kind: 'list'; ordered: boolean; items: AssistantInline[][] };

const INLINE = /\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g;
const BULLET = /^\s*[-*•]\s+(.*)$/;
const NUMBERED = /^\s*\d+[.)]\s+(.*)$/;
const TABLE_ROW = /^\s*\|(.*)\|\s*$/;
const TABLE_RULE = /^\s*\|[\s:|-]*-[\s:|-]*\|\s*$/;

/** Only an address on this site. A protocol-relative or backslash path could lead elsewhere. */
export function isInternalLink(href: string): boolean {
  return /^\/(?![/\\])/.test(href) && !href.includes('\\');
}

/** One line split into plain, bold and link runs. */
function inline(line: string): AssistantInline[] {
  const parts: AssistantInline[] = [];
  let at = 0;
  for (const m of line.matchAll(INLINE)) {
    if (m.index > at) parts.push({ text: line.slice(at, m.index) });
    if (m[1] !== undefined) parts.push({ text: m[1], bold: true });
    // A link to anywhere else keeps its words and loses its address, so a reply can never send someone off the site.
    else parts.push(isInternalLink(m[3]) ? { text: m[2], link: m[3] } : { text: m[2] });
    at = m.index + m[0].length;
  }
  if (at < line.length) parts.push({ text: line.slice(at) });
  return parts;
}

/**
 * Turns a model's reply into a small set of blocks for Angular to render as elements. It never produces HTML, and nothing that looks
 * like a tag or an image is treated as anything but text, so a reply cannot inject markup or load a remote image. Models sometimes write a
 * table even when told not to, and a narrow chat panel has no room for one, so each row becomes a list item and the heading row is dropped.
 */
export function parseAssistantText(text: string): AssistantBlock[] {
  const blocks: AssistantBlock[] = [];
  const lines = text.split('\n');
  const addItem = (ordered: boolean, item: AssistantInline[]) => {
    const last = blocks[blocks.length - 1];
    if (last?.kind === 'list' && last.ordered === ordered) last.items.push(item);
    else blocks.push({ kind: 'list', ordered, items: [item] });
  };
  for (const [at, line] of lines.entries()) {
    if (TABLE_RULE.test(line) || TABLE_RULE.test(lines[at + 1] ?? '')) continue;
    const row = TABLE_ROW.exec(line);
    const bullet = BULLET.exec(line);
    const numbered = bullet ? null : NUMBERED.exec(line);
    if (row) {
      addItem(false, inline(row[1].split('|').map((cell) => cell.trim()).filter(Boolean).join(' · ')));
    } else if (bullet || numbered) {
      addItem(!!numbered, inline((bullet ?? numbered)![1]));
    } else if (line.trim()) {
      blocks.push({ kind: 'paragraph', parts: inline(line.trim().replace(/^#{1,6}\s+/, '')) });
    }
  }
  return blocks;
}
