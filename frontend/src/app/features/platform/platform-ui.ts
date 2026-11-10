import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { BillingCycle, ClientStartMode, InvoiceStatus, PlanView } from '@core/api/platform.api';
import { SelectOption } from '@shared/components/record-drawer/record-drawer.component';

/** The console's page furniture, shared so every screen reads the same. */
export const PLATFORM_STYLES = `
  :host { display: block; min-width: 0; }
  .pf-head { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px 12px; margin-bottom: 16px; }
  .pf-head p { margin: 0; color: var(--erp-color-text-muted); max-width: 720px; line-height: 1.5; }
  .pf-actions { display: flex; flex-wrap: wrap; gap: 8px; }
  .pf-grid { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); margin-bottom: 16px; align-items: start; }
  .pf-card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius: 12px; padding: 16px 18px; min-width: 0; box-sizing: border-box; }
  .pf-card > h2, .pf-card-head h2 { font-size: 15px; font-weight: 600; margin: 0 0 12px; color: var(--erp-color-text-primary); }
  .pf-card-head { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
  .pf-card-head h2 { margin-bottom: 0; }
  .pf-card-head { margin-bottom: 12px; }
  .pf-muted { color: var(--erp-color-text-muted); }
  .pf-small { font-size: 12.5px; }
  .pf-nowrap { white-space: nowrap; }
  .pf-mono { font-family: var(--erp-font-family-mono, monospace); font-size: 12.5px; }
  .pf-facts { display: grid; grid-template-columns: minmax(120px, auto) minmax(0, 1fr); gap: 8px 16px; margin: 0; }
  .pf-facts dt { color: var(--erp-color-text-muted); font-size: 13px; }
  .pf-facts dd { margin: 0; color: var(--erp-color-text-primary); overflow-wrap: anywhere; }
  .pf-scroll { overflow-x: auto; -webkit-overflow-scrolling: touch; margin: 0 -18px; padding: 0 18px; }
  .pf-table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
  .pf-table th { text-align: start; font-size: 12px; font-weight: 600; color: var(--erp-color-text-muted); padding: 6px 12px 6px 0; border-bottom: 1px solid var(--erp-color-border-subtle); white-space: nowrap; }
  .pf-table td { padding: 8px 12px 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); vertical-align: top; color: var(--erp-color-text-primary); }
  .pf-table tr:last-child td { border-bottom: 0; }
  .pf-table .num { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; padding-inline-end: 0; }
  .pf-list { list-style: none; margin: 0; padding: 0; }
  .pf-list li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); min-width: 0; }
  .pf-list li:last-child { border-bottom: 0; }
  .pf-empty { color: var(--erp-color-text-muted); font-size: 13px; padding: 8px 0; margin: 0; }
  @media (max-width: 767px) {
    .pf-grid { grid-template-columns: minmax(0, 1fr); }
    .pf-card { padding: 14px; }
    .pf-scroll { margin: 0 -14px; padding: 0 14px; }
    .pf-facts { grid-template-columns: minmax(96px, auto) minmax(0, 1fr); gap: 6px 12px; }
  }
`;

export const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: 'planRequest', label: 'Asked for a plan change' },
  { value: 'trialing', label: 'On trial' }, { value: 'active', label: 'Active' }, { value: 'pastDue', label: 'Payment overdue' }, { value: 'expired', label: 'Trial ended' },
  { value: 'suspended', label: 'Suspended' }, { value: 'cancelled', label: 'Cancelled' }, { value: 'none', label: 'No plan' },
];

export { statusWords } from '@core/format/subscription';
export const ACCESS_WORDS: Record<string, string> = { full: 'Full access', readOnly: 'View only', blocked: 'Blocked' };
export function cycleWords(cycle: BillingCycle | null | undefined): string { return cycle === 'annual' ? 'Annual' : cycle === 'monthly' ? 'Monthly' : ''; }
export function invoiceTone(invoice: { status: InvoiceStatus; isOverdue: boolean }): string { return invoice.isOverdue ? 'overdue' : invoice.status; }

export const CYCLE_OPTIONS: SelectOption[] = [{ value: 'monthly', label: 'Monthly' }, { value: 'annual', label: 'Annual' }];
export const START_OPTIONS: { value: ClientStartMode; label: string }[] = [{ value: 'trial', label: 'Trial' }, { value: 'active', label: 'Active (paid from today)' }, { value: 'complimentary', label: 'Free of charge' }];

/** Plans to offer when assigning: active ones, plus the current one when it has since been archived. */
export function planOptions(plans: PlanView[], keep?: string | null): SelectOption[] {
  return plans.filter((p) => p.isActive || p.id === keep).map((p) => ({ value: p.id, label: p.name + (p.isActive ? '' : ' (archived)') + (p.isPublic ? '' : ' (private)') }));
}

export interface OneTimeSecret {
  title: string;
  /** Who or what the password is for. */
  facts: { label: string; value: string }[];
  /** null: the email already has an account, which keeps its own password. */
  password: string | null;
}

/** A temporary password is shown once. There is no second chance to read it, and the dialog says so. */
@Component({
  selector: 'erp-one-time-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzAlertModule, NzButtonModule, NzIconModule, NzModalModule],
  styleUrl: './platform-ui.scss',
  templateUrl: './platform-ui.html',
})
export class OneTimePasswordComponent {
  readonly secret = input<OneTimeSecret | null>(null);
  readonly actionLabel = input('Done');
  readonly closed = output<void>();
  readonly copied = signal(false);
  readonly copyFailed = signal(false);

  close(): void {
    this.copied.set(false);
    this.copyFailed.set(false);
    this.closed.emit();
  }

  copy(text: string): void {
    this.copyFailed.set(false);
    const done = () => this.copied.set(true);
    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(done, () => this.copyFallback(text, done));
    } else {
      this.copyFallback(text, done);
    }
  }

  /** Older browsers and pages not served over HTTPS have no clipboard API. If this fails too, the person copies it by hand. */
  private copyFallback(text: string, done: () => void): void {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    if (document.execCommand('copy')) done();
    else this.copyFailed.set(true);
    area.remove();
  }
}
