import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { BillingCycle, PublicPlan } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { limitText, sellingPoints, yearlySaving } from '@core/format/subscription';

/** One plan on a price list: the public registration page and a client's billing page show the same card. */
@Component({
  selector: 'erp-plan-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule],
  styles: [`
    :host { display: block; min-width: 0; }
    .plan { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); position: relative; display: flex; flex-direction: column; height: 100%; padding: 20px; border-radius: 14px; }
    .plan.recommended { border: 2px solid var(--erp-color-primary); padding: 19px; }
    .plan.selected { box-shadow: 0 0 0 3px var(--erp-color-primary-subtle); border-color: var(--erp-color-primary); }
    .badge { position: absolute; top: -11px; inset-inline-start: 16px; background: var(--erp-color-primary); color: var(--erp-color-on-primary, #fff); font-size: 11px; font-weight: 700; letter-spacing: .03em; padding: 2px 10px; border-radius: 9999px; }
    h3 { margin: 0; font-size: 17px; font-weight: 700; color: var(--erp-color-text-primary); }
    .desc { margin: 4px 0 12px; color: var(--erp-color-text-muted); font-size: 13px; line-height: 1.45; min-height: 38px; }
    .price { font-size: 26px; font-weight: 800; color: var(--erp-color-text-primary); letter-spacing: -.02em; line-height: 1.15; overflow-wrap: anywhere; }
    .price small { font-size: 13px; font-weight: 500; color: var(--erp-color-text-muted); letter-spacing: 0; }
    .saving { min-height: 20px; margin-top: 2px; font-size: 12.5px; font-weight: 600; color: var(--erp-color-success-text); }
    .trial { margin-top: 8px; font-size: 13px; color: var(--erp-color-text-secondary); }
    ul { list-style: none; margin: 12px 0 16px; padding: 12px 0 0; border-top: 1px solid var(--erp-color-border-subtle); display: flex; flex-direction: column; gap: 6px; flex: 1; }
    li { display: flex; gap: 8px; align-items: flex-start; font-size: 13px; color: var(--erp-color-text-secondary); line-height: 1.4; }
    li span[nz-icon] { color: var(--erp-color-primary); margin-top: 3px; flex-shrink: 0; }
    button { white-space: normal; height: auto; min-height: 32px; overflow-wrap: anywhere; }
  `],
  template: `
    <div class="plan" [class.recommended]="plan().isRecommended" [class.selected]="selected()" [attr.data-testid]="'plan-card-' + plan().code">
      @if (plan().isRecommended) { <span class="badge">Recommended</span> }
      <h3>{{ plan().name }}</h3>
      <p class="desc">{{ plan().description }}</p>
      <div class="price" data-testid="plan-price">{{ price() }} <small>{{ cycle() === 'annual' ? 'a year' : 'a month' }}</small></div>
      <div class="saving">@if (savingText(); as s) { {{ s }} }</div>
      <div class="trial">{{ plan().trialDays > 0 ? plan().trialDays + ' days free trial' : 'No trial: starts straight away' }}</div>
      <ul>
        <li><span nz-icon nzType="check" aria-hidden="true"></span>{{ users() }}</li>
        <li><span nz-icon nzType="check" aria-hidden="true"></span>{{ entities() }}</li>
        <li><span nz-icon nzType="check" aria-hidden="true"></span>{{ plan().modules.length ? plan().modules.length + (plan().modules.length === 1 ? ' module' : ' modules') + ' included' : 'Every module included' }}</li>
        @for (f of features(); track f) { <li><span nz-icon nzType="check" aria-hidden="true"></span>{{ f }}</li> }
      </ul>
      <button nz-button [nzType]="plan().isRecommended || selected() ? 'primary' : 'default'" nzBlock type="button" [disabled]="disabled()" (click)="choose.emit(plan())" [attr.data-testid]="'plan-choose-' + plan().code">{{ actionLabel() }}</button>
    </div>`,
})
export class PlanCardComponent {
  readonly plan = input.required<PublicPlan>();
  readonly cycle = input<BillingCycle>('monthly');
  readonly actionLabel = input('Choose');
  readonly selected = input(false);
  readonly disabled = input(false);
  readonly choose = output<PublicPlan>();

  readonly price = computed(() => {
    const p = this.plan();
    return displayMoney(this.cycle() === 'annual' ? p.annualPrice : p.monthlyPrice, p.currency);
  });
  readonly savingText = computed(() => {
    const p = this.plan();
    const saving = this.cycle() === 'annual' ? yearlySaving(p.monthlyPrice, p.annualPrice) : null;
    return saving ? `You save ${displayMoney(saving.amount, p.currency)} a year (${saving.percent}%)` : null;
  });
  readonly features = computed(() => sellingPoints(this.plan().features));
  readonly users = computed(() => limitText(this.plan().maxUsers, 'user', 'users'));
  readonly entities = computed(() => limitText(this.plan().maxLegalEntities, 'legal entity', 'legal entities'));
}
