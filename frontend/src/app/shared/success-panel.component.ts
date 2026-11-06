import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { celebrate } from '../core/format/celebrate';
import { I18nService } from '../core/i18n/i18n.service';

/**
 * The outcome of something that matters, such as money received: it stays on the page until the person closes it or leaves.
 *   <erp-success-panel [title]="..." [detail]="...">
 *     <button actions nz-button nzType="primary">Print receipt</button>
 *     <span loop>Next: <a>see what they still owe</a></span>
 *   </erp-success-panel>
 * Say what happened with specifics in `title`, offer the next action in `[actions]` and leave one open loop in `[loop]`.
 * Lower stakes use SuccessToastService. `celebrate` is for rare firsts only, such as the first invoice paid, never for routine saves.
 */
@Component({
  selector: 'erp-success-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule],
  template: `
    <section class="erp-success-panel" role="status" data-testid="success-panel">
      <span class="erp-success-panel__icon" nz-icon nzType="check-circle" nzTheme="fill" aria-hidden="true"></span>
      <div class="erp-success-panel__body">
        <h2 class="erp-success-panel__title" data-testid="success-title">{{ title() }}</h2>
        @if (detail()) { <p class="erp-success-panel__detail" data-testid="success-detail">{{ detail() }}</p> }
        <div class="erp-success-panel__actions"><ng-content select="[actions]" /></div>
        <p class="erp-success-panel__loop"><ng-content select="[loop]" /></p>
      </div>
      <button nz-button nzType="text" nzSize="small" type="button" class="erp-success-panel__close" [attr.aria-label]="closeLabel()" (click)="dismissed.emit()" data-testid="success-close">
        <span nz-icon nzType="close"></span>
      </button>
    </section>`,
})
export class SuccessPanelComponent {
  private readonly i18n = inject(I18nService);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly title = input.required<string>();
  readonly detail = input<string | null>(null);
  readonly celebrate = input(false);
  readonly dismissed = output<void>();

  constructor() {
    effect(() => { if (this.celebrate()) celebrate(this.host.nativeElement); });
  }

  closeLabel(): string { return this.i18n.t('ui.common.close', 'Close'); }
}
