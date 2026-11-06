const PIECES = 36;
const LIFETIME_MS = 1800;
const HUES = ['--erp-color-viz-1', '--erp-color-viz-2', '--erp-color-viz-3', '--erp-color-viz-4', '--erp-color-viz-5', '--erp-color-primary'];

/**
 * A short burst of confetti from an element, for rare firsts only. Nothing happens under reduced motion. It adds no dependency and
 * cleans up after itself; the styles are in theme/_success.scss.
 */
export function celebrate(from: HTMLElement): void {
  if (typeof document === 'undefined') return;
  const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches || document.documentElement.getAttribute('data-motion') === 'reduced';
  if (reduced) return;
  const box = from.getBoundingClientRect();
  const layer = document.createElement('div');
  layer.className = 'erp-confetti';
  layer.setAttribute('aria-hidden', 'true');
  layer.setAttribute('data-testid', 'confetti');
  for (let i = 0; i < PIECES; i++) {
    const piece = document.createElement('i');
    piece.style.left = `${box.left + box.width * Math.random()}px`;
    piece.style.top = `${box.top + 16}px`;
    piece.style.background = `var(${HUES[i % HUES.length]})`;
    piece.style.setProperty('--dx', `${(Math.random() - 0.5) * 320}px`);
    piece.style.setProperty('--dy', `${120 + Math.random() * 260}px`);
    piece.style.setProperty('--turn', `${360 + Math.random() * 720}deg`);
    piece.style.animationDelay = `${Math.random() * 160}ms`;
    layer.appendChild(piece);
  }
  document.body.appendChild(layer);
  setTimeout(() => layer.remove(), LIFETIME_MS);
}

const FIRST_PAYMENT_KEY = 'erp.celebrated.firstPayment';

/** True once for a workspace: the moment its very first customer payment is applied to an invoice. */
export function isFirstPaymentMoment(workspaceId: string | null | undefined, totalReceipts: number, appliedToInvoice: boolean): boolean {
  if (totalReceipts !== 1 || !appliedToInvoice) return false;
  const key = `${FIRST_PAYMENT_KEY}:${workspaceId ?? ''}`;
  try {
    if (localStorage.getItem(key)) return false;
    localStorage.setItem(key, '1');
  } catch { /* storage unavailable: it may celebrate again, which is harmless */ }
  return true;
}
