import { PHRASES_AR } from './phrases.ar';

const SKIP_TAGS = new Set(['SCRIPT', 'STYLE', 'INPUT', 'TEXTAREA', 'CODE', 'PRE', 'OPTION']);
const ATTRS = ['placeholder', 'title', 'aria-label'];

/**
 * Swaps known English UI text for Arabic in the live DOM while the language is Arabic, and restores it when switching back.
 * Covers templates, table columns, form labels, report definitions and ng-zorro internals without per-string markup.
 * Only exact (trimmed) matches from <see cref="PHRASES_AR"/> are replaced, so user data is left alone unless it equals a UI phrase.
 * Anything inside an element marked `data-no-i18n` is skipped.
 */
export class DomTranslator {
  private observer?: MutationObserver;
  private readonly originalText = new Map<Text, string>();
  private readonly originalAttr = new Map<Element, Map<string, string>>();
  private scheduled = false;

  start(root: HTMLElement): void {
    this.stop(false);
    this.walk(root);
    this.observer = new MutationObserver(() => this.schedule(root));
    this.observer.observe(root, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  }

  /** Stops watching; with restore, puts the English text back. */
  stop(restore = true): void {
    this.observer?.disconnect();
    this.observer = undefined;
    if (!restore) return;
    for (const [node, text] of this.originalText) if (node.isConnected) node.nodeValue = text;
    for (const [el, attrs] of this.originalAttr) if (el.isConnected) for (const [name, value] of attrs) el.setAttribute(name, value);
    this.originalText.clear();
    this.originalAttr.clear();
  }

  private schedule(root: HTMLElement): void {
    if (this.scheduled) return;
    this.scheduled = true;
    queueMicrotask(() => {
      this.scheduled = false;
      if (!this.observer) return;
      this.observer.disconnect();
      this.walk(root);
      this.observer.observe(root, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ATTRS });
      this.prune();
    });
  }

  private walk(node: Node): void {
    if (node.nodeType === Node.TEXT_NODE) { this.text(node as Text); return; }
    if (node.nodeType !== Node.ELEMENT_NODE) return;
    const el = node as Element;
    if (SKIP_TAGS.has(el.tagName) || el.hasAttribute('data-no-i18n')) return;
    for (const a of ATTRS) {
      const v = el.getAttribute(a);
      const ar = v ? this.lookup(v) : null;
      if (ar) {
        let saved = this.originalAttr.get(el);
        if (!saved) this.originalAttr.set(el, (saved = new Map()));
        if (!saved.has(a)) saved.set(a, v!);
        el.setAttribute(a, ar);
      }
    }
    for (let child = el.firstChild; child; child = child.nextSibling) this.walk(child);
  }

  private text(node: Text): void {
    const value = node.nodeValue;
    if (!value || !value.trim()) return;
    const ar = this.lookup(value);
    if (!ar) return;
    this.originalText.set(node, value); // always the latest English value, in case a binding re-rendered it
    node.nodeValue = value.replace(value.trim(), ar); // keep surrounding whitespace
  }

  private lookup(value: string): string | null {
    const key = value.trim().replace(/\s+/g, ' ');
    return PHRASES_AR[key] ?? PHRASES_AR[key.replace(/^[A-Z]/, (c) => c.toUpperCase())] ?? this.titleCase(key);
  }

  /** "in progress" / "partlyPaid" style values rendered by humanize(): try the capitalised, spaced form. */
  private titleCase(key: string): string | null {
    const spaced = key.replace(/([a-z])([A-Z])/g, '$1 $2');
    const cap = spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
    return cap !== key ? (PHRASES_AR[cap] ?? null) : null;
  }

  private prune(): void {
    if (this.originalText.size < 5000) return;
    for (const node of [...this.originalText.keys()]) if (!node.isConnected) this.originalText.delete(node);
    for (const el of [...this.originalAttr.keys()]) if (!el.isConnected) this.originalAttr.delete(el);
  }
}
