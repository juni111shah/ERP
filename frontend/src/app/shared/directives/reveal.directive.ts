import { AfterViewInit, Directive, ElementRef, OnDestroy, inject, input } from '@angular/core';

/**
 * Icon-first controls that reveal their label on hover / keyboard focus (styles: `.erp-reveal` in src/styles.scss).
 *
 * Put `erpReveal` on a toolbar (or any container) and every icon + text `nz-button` inside it, including projected and later-added
 * ones, collapses to its icon and slides its label out on hover or keyboard focus. Optional filter selects (`nz-select` with
 * nzAllowClear) collapse to a filter icon and open up on hover, focus, or while they hold a value.
 * Put it on a single button to opt that button in. Primary, link, block and icon-only buttons are left alone; add
 * `data-reveal="off"` to keep any control expanded, or `data-reveal="on"` to force it (e.g. a primary button).
 * The label stays in the DOM (only visually collapsed), so it remains the accessible name; touch devices show labels.
 */
@Directive({ selector: '[erpReveal]', standalone: true })
export class RevealDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  /** Also collapse optional selects inside the container. */
  readonly erpRevealSelects = input(true);
  private observer?: MutationObserver;
  private frame = 0;

  ngAfterViewInit(): void {
    this.scan();
    if (typeof MutationObserver === 'undefined') return;
    this.observer = new MutationObserver(() => {
      if (this.frame) return;
      this.frame = requestAnimationFrame(() => { this.frame = 0; this.scan(); });
    });
    this.observer.observe(this.host.nativeElement, { childList: true, subtree: true });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    if (this.frame) cancelAnimationFrame(this.frame);
  }

  private scan(): void {
    const el = this.host.nativeElement as HTMLElement;
    const buttons = el.matches('button, a.ant-btn') ? [el] : Array.from(el.querySelectorAll<HTMLElement>('button.ant-btn, a.ant-btn'));
    for (const b of buttons) this.markButton(b);
    if (this.erpRevealSelects() && !el.matches('button')) {
      for (const s of Array.from(el.querySelectorAll<HTMLElement>('nz-select'))) this.markSelect(s);
    }
  }

  private markButton(b: HTMLElement): void {
    const mode = b.getAttribute('data-reveal');
    if (mode === 'off' || b.closest('[data-reveal="off"]')) { b.classList.remove('erp-reveal'); return; }
    const hasIcon = !!b.querySelector(':scope > .anticon, :scope > [nz-icon]');
    const hasText = (b.textContent ?? '').trim().length > 0;
    const excluded = b.matches('.ant-btn-primary, .ant-btn-link, .ant-btn-block, .ant-btn-icon-only');
    const on = hasIcon && hasText && (mode === 'on' || !excluded);
    b.classList.toggle('erp-reveal', on);
  }

  private markSelect(s: HTMLElement): void {
    const mode = s.getAttribute('data-reveal');
    const optional = s.hasAttribute('nzallowclear') || s.hasAttribute('nzAllowClear');
    const on = mode === 'on' || (mode !== 'off' && optional && !s.closest('form'));
    s.classList.toggle('erp-reveal-select', on);
  }
}
