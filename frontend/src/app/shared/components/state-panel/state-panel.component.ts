import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';

/**
 * What a page shows instead of a blank area: nothing run yet, nothing found, or a failed load.
 * With an action label it offers the next step, so the page is never a dead end.
 */
@Component({
  selector: 'erp-state-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule],
  styles: [`
    :host { display: block; }
    .panel { display: flex; flex-direction: column; align-items: center; text-align: center; gap: var(--erp-space-2, 8px); padding: var(--erp-space-12, 48px) var(--erp-space-6, 24px);
      background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: var(--erp-radius-card, 8px); }
    .icon { width: 48px; height: 48px; display: grid; place-items: center; border-radius: 50%; font-size: 22px; background: var(--erp-color-bg-subtle); color: var(--erp-color-text-muted); }
    .panel[data-kind='error'] .icon { background: var(--erp-color-error-bg); color: var(--erp-color-error-text); }
    h3 { margin: var(--erp-space-2, 8px) 0 0; font-size: 16px; font-weight: 600; color: var(--erp-color-text-primary); }
    p { margin: 0; max-width: 460px; color: var(--erp-color-text-muted); }
    button { margin-top: var(--erp-space-3, 12px); }
  `],
  template: `
    <div class="panel" [attr.data-kind]="kind()" [attr.role]="kind() === 'error' ? 'alert' : null" data-testid="state-panel">
      <span class="icon" nz-icon [nzType]="icon() ?? (kind() === 'error' ? 'warning' : 'inbox')" aria-hidden="true"></span>
      <h3>{{ title() }}</h3>
      @if (hint()) { <p>{{ hint() }}</p> }
      @if (actionLabel()) { <button nz-button nzType="primary" type="button" (click)="action.emit()" data-testid="state-panel-action">{{ actionLabel() }}</button> }
    </div>`,
})
export class StatePanelComponent {
  readonly kind = input<'empty' | 'error'>('empty');
  readonly title = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly icon = input<string | null>(null);
  readonly actionLabel = input<string | null>(null);
  readonly action = output<void>();
}
