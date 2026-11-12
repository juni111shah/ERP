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
  styleUrl: './plan-card.component.scss',
  templateUrl: './plan-card.component.html',
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
