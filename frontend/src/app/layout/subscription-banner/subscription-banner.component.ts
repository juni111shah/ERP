import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { subscriptionBanner } from '@core/format/subscription';
import { SessionStore } from '@core/session/session.store';

/** One line above the page when the trial is ending, a payment is late or changes are paused. Otherwise nothing at all. */
@Component({
  selector: 'erp-subscription-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzIconModule],
  styles: [`
    :host { display: block; flex-shrink: 0; }
    .banner { display: flex; align-items: center; flex-wrap: wrap; gap: 2px 10px; padding: 7px 20px; font-size: 13px; line-height: 1.4; color: var(--erp-color-text-primary); border-bottom: 1px solid var(--erp-color-border-subtle); }
    .banner[data-tone='info'] { background: var(--erp-color-info-bg); border-color: var(--erp-color-info-border); }
    .banner[data-tone='warning'] { background: var(--erp-color-warning-bg); border-color: var(--erp-color-warning-border); }
    .banner[data-tone='error'] { background: var(--erp-color-error-bg); border-color: var(--erp-color-error-border); }
    .text { flex: 1 1 200px; min-width: 0; }
    a { font-weight: 600; white-space: nowrap; }
    @media (max-width: 767px) { .banner { padding: 6px 12px; font-size: 12.5px; } }
  `],
  template: `
    @if (banner(); as b) {
      <div class="banner" [attr.data-tone]="b.tone" role="status" data-testid="subscription-banner">
        <span nz-icon [nzType]="b.tone === 'info' ? 'clock-circle' : 'warning'" aria-hidden="true"></span>
        <span class="text">{{ b.text }}@if (!canSee()) { Please ask your administrator. }</span>
        @if (canSee()) { <a routerLink="/admin/billing" data-testid="subscription-banner-link">Subscription and billing</a> }
      </div>
    }`,
})
export class SubscriptionBannerComponent {
  private readonly session = inject(SessionStore);
  readonly banner = computed(() => subscriptionBanner(this.session.me()?.subscription));
  readonly canSee = computed(() => this.session.hasPermission('core.billing.read'));
}
