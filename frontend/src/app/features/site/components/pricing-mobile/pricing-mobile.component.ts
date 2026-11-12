import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { InViewDirective, TiltDirective, prefersReducedMotion } from '../../pricing-fx';
import { PhoneScreen, carouselOffset } from '../../pricing-mockups';
import { PhoneComponent } from '../phone/phone.component';

interface Slide { key: PhoneScreen; title: string; text: string; icon: string; }

/** Only what the mobile app really does. */
const SLIDES: Slide[] = [
  { key: 'overview', icon: 'dashboard', title: 'The business at a glance', text: 'Sales, what customers owe and what needs attention, on the same figures as the office.' },
  { key: 'invoices', icon: 'file-text', title: 'Invoices, receipts and customers', text: 'Look up a customer or invoice while you are with them, and record what they paid.' },
  { key: 'approvals', icon: 'check-square', title: 'Approve from anywhere', text: 'Purchase orders, expenses and leave do not have to wait until you are back at a desk.' },
  { key: 'hr', icon: 'calendar', title: 'My HR for every employee', text: 'People check their leave balance, ask for time off and see their payslips themselves.' },
  { key: 'alerts', icon: 'bell', title: 'Your notifications, wherever you are', text: 'See what needs you from the list in the app, signed in as safely as on the web, with two-factor codes.' },
];
const AUTOPLAY_MS = 3800;

/** The mobile app, presented as phones on a turning 3D carousel. Choosing a line or a phone turns it to the front. */
@Component({
  selector: 'erp-pricing-mobile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule, KeyboardClickDirective, PhoneComponent, InViewDirective, TiltDirective],
  styleUrl: './pricing-mobile.component.scss',
  templateUrl: './pricing-mobile.component.html',
})
export class PricingMobileComponent implements OnInit, OnDestroy {
  readonly slides = SLIDES;
  readonly active = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;
  private held = false;

  readonly placed = computed(() => {
    const narrow = typeof window !== 'undefined' && window.innerWidth < 768;
    const spread = narrow ? 118 : 178;
    return this.slides.map((_, i) => {
      const offset = carouselOffset(i, this.active(), this.slides.length);
      const away = Math.abs(offset);
      return {
        transform: `translateX(${offset * spread}px) translateZ(${-away * 150}px) rotateY(${-offset * 32}deg) scale(${narrow ? 0.94 : 1})`,
        opacity: 1,
        dim: away === 0 ? 0 : away === 1 ? 0.32 : 0.62,
      };
    });
  });

  ngOnInit(): void {
    if (prefersReducedMotion()) return;
    this.timer = setInterval(() => {
      if (!this.held && !document.hidden) this.active.update((a) => (a + 1) % this.slides.length);
    }, AUTOPLAY_MS);
  }

  ngOnDestroy(): void { if (this.timer) clearInterval(this.timer); }

  select(i: number): void { this.active.set(i); }
  hold(on: boolean): void { this.held = on; }
}
