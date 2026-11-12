import { AfterViewInit, Directive, ElementRef, NgZone, OnDestroy, inject } from '@angular/core';

export function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
}

/**
 * Plays an element's entrance the first time it scrolls into view, using the classes fx-wait and fx-in.
 * Content starts visible, and is only held back once this has confirmed it can bring it back, so a browser
 * without the observer, or a person who asked for reduced motion, simply sees the finished page.
 */
@Directive({ selector: '[erpInView]', standalone: true })
export class InViewDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  private observer?: IntersectionObserver;

  ngAfterViewInit(): void {
    if (prefersReducedMotion() || typeof IntersectionObserver === 'undefined') return;
    const el = this.host.nativeElement as HTMLElement;
    el.classList.add('fx-wait');
    this.observer = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return;
      el.classList.remove('fx-wait');
      el.classList.add('fx-in');
      this.observer?.disconnect();
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.05 });
    this.observer.observe(el);
  }

  ngOnDestroy(): void { this.observer?.disconnect(); }
}

/** Follows the pointer: sets --tilt-x and --tilt-y on the element for its CSS transform. Only with a real pointer, and never with reduced motion. */
@Directive({ selector: '[erpTilt]', standalone: true })
export class TiltDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly zone = inject(NgZone);
  private frame = 0;
  private stop: (() => void) | null = null;

  ngAfterViewInit(): void {
    if (prefersReducedMotion() || !window.matchMedia?.('(hover: hover) and (pointer: fine)').matches) return;
    const el = this.host.nativeElement as HTMLElement;
    const strength = 9;
    // Outside Angular: this runs on every pointer move and changes nothing but two style values.
    this.zone.runOutsideAngular(() => {
      const move = (e: PointerEvent) => {
        if (this.frame) return;
        this.frame = requestAnimationFrame(() => {
          this.frame = 0;
          const r = el.getBoundingClientRect();
          if (!r.width || !r.height) return;
          el.style.setProperty('--tilt-y', `${(((e.clientX - r.left) / r.width) - 0.5) * strength}deg`);
          el.style.setProperty('--tilt-x', `${(0.5 - ((e.clientY - r.top) / r.height)) * strength}deg`);
        });
      };
      const leave = () => { el.style.setProperty('--tilt-x', '0deg'); el.style.setProperty('--tilt-y', '0deg'); };
      el.addEventListener('pointermove', move, { passive: true });
      el.addEventListener('pointerleave', leave);
      this.stop = () => { el.removeEventListener('pointermove', move); el.removeEventListener('pointerleave', leave); };
    });
  }

  ngOnDestroy(): void {
    this.stop?.();
    if (this.frame) cancelAnimationFrame(this.frame);
  }
}

/**
 * Shows a fixed-size drawing at whatever width the page gives it, as a real screen would look from further away.
 * Sets the drawing's scale and the height it then takes up.
 */
@Directive({ selector: '[erpFit]', standalone: true })
export class FitDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  private observer?: ResizeObserver;

  ngAfterViewInit(): void {
    const el = this.host.nativeElement as HTMLElement;
    const inner = el.firstElementChild as HTMLElement | null;
    if (!inner) return;
    const apply = () => {
      const scale = el.clientWidth / inner.offsetWidth;
      inner.style.transform = `scale(${scale})`;
      el.style.height = `${inner.offsetHeight * scale}px`;
    };
    apply();
    if (typeof ResizeObserver !== 'undefined') {
      this.observer = new ResizeObserver(apply);
      this.observer.observe(el);
    }
  }

  ngOnDestroy(): void { this.observer?.disconnect(); }
}
