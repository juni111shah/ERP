import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { I18nService } from '../core/i18n/i18n.service';

/** A device with a real pointer can reveal actions on hover. Anything else gets the kebab menu. */
const CAN_HOVER = '(hover: hover) and (pointer: fine)';

/**
 * The actions of one table row. With a mouse they appear when the row is hovered or takes keyboard focus (theme: .erp-row-actions__inline);
 * on touch they sit behind a kebab menu. erp-paged-table wraps its `actions` column in this; a hand-written table wraps its buttons:
 *   <td><erp-row-actions><button nz-button nzSize="small">Edit</button></erp-row-actions></td>
 */
@Component({
  selector: 'erp-row-actions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, NzButtonModule, NzIconModule, NzPopoverModule],
  host: { '(click)': '$event.stopPropagation()', '(keydown.enter)': '$event.stopPropagation()', '(keydown.space)': '$event.stopPropagation()' },
  template: `
    <ng-template #actions><ng-content /></ng-template>
    @if (canHover()) {
      <div class="erp-row-actions__inline"><ng-container *ngTemplateOutlet="actions" /></div>
    } @else {
      <button nz-button nzType="text" nzSize="small" type="button" class="erp-row-actions__kebab" nz-popover nzPopoverTrigger="click" nzPopoverPlacement="bottomRight"
              nzPopoverOverlayClassName="erp-row-actions__overlay" [nzPopoverContent]="menu" [nzPopoverVisible]="open()" (nzPopoverVisibleChange)="open.set($event)"
              [attr.aria-label]="label()" [attr.aria-expanded]="open()" data-testid="row-actions-menu">
        <span nz-icon nzType="more"></span>
      </button>
      <ng-template #menu>
        <div class="erp-row-actions__menu" (click)="closeAfter($event)" (keydown.escape)="open.set(false)"><ng-container *ngTemplateOutlet="actions" /></div>
      </ng-template>
    }`,
})
export class RowActionsComponent {
  private readonly i18n = inject(I18nService);
  private readonly query = typeof matchMedia === 'undefined' ? null : matchMedia(CAN_HOVER);
  readonly canHover = signal(this.query?.matches ?? true);
  readonly open = signal(false);

  constructor() {
    const onChange = (e: MediaQueryListEvent) => this.canHover.set(e.matches);
    this.query?.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => this.query?.removeEventListener('change', onChange));
  }

  label(): string { return this.i18n.t('table.rowActions', 'Row actions'); }

  /** The menu closes once an action runs. A confirmation pops up from its button, so that one keeps the menu open behind it. */
  closeAfter(e: Event): void {
    const target = e.target instanceof Element ? e.target : null;
    if (target?.closest('button, a') && !target.closest('[nz-popconfirm]')) this.open.set(false);
  }
}
