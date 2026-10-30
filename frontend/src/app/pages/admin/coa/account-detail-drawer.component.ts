import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AccountNodeDto } from '../../../core/api/admin.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../../shared/has-permission.directive';
import { RevealDirective } from '../../../shared/reveal.directive';
import { AccountTreeStore } from './account-tree.store';

/**
 * Read-only details of one leaf account in a side drawer (the same drawer style as the account edit form). Opened from the
 * step-by-step chart when an account without sub-accounts is chosen; closing it leaves the columns as they were.
 * The parent owns the actions: `edit` swaps this drawer for the edit form, `addSub` for the new-account form, `toggleActive`
 * (de)activates in place.
 */
@Component({
  selector: 'erp-account-detail-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzDrawerModule, NzButtonModule, NzIconModule, NzPopconfirmModule, TranslatePipe, HasPermissionDirective, RevealDirective],
  template: `
    <nz-drawer [nzVisible]="!!account()" [nzWidth]="560" [nzTitle]="'coa.detailTitle' | translate: 'Account details'" (nzOnClose)="closed.emit()" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        @if (account(); as a) {
          <div class="coa-dd" data-testid="coa-detail">
            <div class="coa-dd-head">
              <div class="coa-dd-name"><span class="coa-dd-code">{{ a.code }}</span> {{ a.name }}</div>
              <span class="coa-dd-tags">
                @if (!a.isPostable) { <span class="coa-dd-tag">{{ 'coa.tag.header' | translate: 'Header' }}</span> }
                @if (a.isBank) { <span class="coa-dd-tag is-info">{{ 'coa.tag.bank' | translate: 'Bank' }}</span> }
                @if (a.isSystem) { <span class="coa-dd-tag is-system">{{ 'coa.tag.system' | translate: 'System' }}</span> }
                @if (!a.isActive) { <span class="coa-dd-tag is-off">{{ 'coa.tag.inactive' | translate: 'Inactive' }}</span> }
              </span>
            </div>

            <div class="coa-dd-actions" erpReveal *erpHasPermission="'finance.accounts.write'">
              <button nz-button nzSize="small" type="button" (click)="edit.emit(a)" data-testid="coa-detail-edit"
                      [attr.aria-label]="'common.edit' | translate: 'Edit'" [title]="'common.edit' | translate: 'Edit'"><span nz-icon nzType="edit"></span>{{ 'common.edit' | translate: 'Edit' }}</button>
              <button nz-button nzSize="small" type="button" (click)="addSub.emit(a)" data-testid="coa-detail-add-sub"
                      [attr.aria-label]="'coa.addSub' | translate: 'Add sub-account'" [title]="'coa.addSub' | translate: 'Add sub-account'"><span nz-icon nzType="plus"></span>{{ 'coa.addSub' | translate: 'Add sub-account' }}</button>
              @if (!a.isSystem) {
                <button nz-button nzSize="small" type="button" [nzDanger]="a.isActive" nz-popconfirm
                        [nzPopconfirmTitle]="(a.isActive ? 'coa.confirmDeactivate' : 'coa.confirmActivate') | translate: (a.isActive ? 'Deactivate this account? It stays in history but cannot be used on new entries.' : 'Activate this account again?')"
                        (nzOnConfirm)="toggleActive.emit(a)" data-testid="coa-detail-toggle-active"
                        [attr.aria-label]="(a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate')"
                        [title]="(a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate')">
                  <span nz-icon [nzType]="a.isActive ? 'stop' : 'check-circle'"></span>{{ (a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate') }}
                </button>
              }
            </div>

            <dl class="coa-dd-fields" data-testid="coa-detail-fields">
              <dt>{{ 'coa.f.code' | translate: 'Code' }}</dt><dd class="coa-dd-code" data-testid="coa-detail-code">{{ a.code }}</dd>
              <dt>{{ 'coa.f.name' | translate: 'Name' }}</dt><dd data-testid="coa-detail-name">{{ a.name }}</dd>
              <dt>{{ 'coa.f.type' | translate: 'Type' }}</dt><dd>{{ typeLabel()(a.type) }}</dd>
              <dt>{{ 'coa.f.subcategory' | translate: 'Sub-category' }}</dt><dd data-testid="coa-detail-sub">{{ subcategory() || '—' }}</dd>
              <dt>{{ 'coa.f.reportingGroup' | translate: 'Reporting group' }}</dt><dd>{{ a.reportingGroup || '—' }}</dd>
              <dt>{{ 'coa.f.parent' | translate: 'Parent account' }}</dt>
              <dd data-testid="coa-detail-parent">{{ parentLabel() || (a.parentId ? '…' : ('coa.noParent' | translate: 'None (top level)')) }}</dd>
              <dt>{{ 'coa.f.postable' | translate: 'Postable' }}</dt><dd>{{ yesNo(a.isPostable) }}</dd>
              <dt>{{ 'coa.f.bank' | translate: 'Bank / cash' }}</dt><dd>{{ yesNo(a.isBank) }}</dd>
              <dt>{{ 'coa.f.currency' | translate: 'Currency' }}</dt><dd>{{ a.currencyCode || ('coa.baseCurrency' | translate: 'Base currency') }}</dd>
              @if (a.systemRole) { <dt>{{ 'coa.f.systemRole' | translate: 'System role' }}</dt><dd>{{ a.systemRole }}</dd> }
              <dt>{{ 'coa.f.status' | translate: 'Status' }}</dt>
              <dd data-testid="coa-detail-status"><span class="coa-dd-status" [class.is-off]="!a.isActive">{{ (a.isActive ? 'coa.status.active' : 'coa.status.inactive') | translate: (a.isActive ? 'Active' : 'Inactive') }}</span></dd>
              @if (a.balance != null) {
                <dt>{{ 'coa.f.balance' | translate: 'Balance' }}</dt><dd class="coa-dd-balance" [class.is-negative]="a.balance < 0" data-testid="coa-detail-balance">{{ money(a.balance) }}</dd>
              }
            </dl>
          </div>
        }
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button type="button" (click)="closed.emit()" data-testid="coa-detail-close">{{ 'common.close' | translate: 'Close' }}</button>
    </ng-template>`,
  styles: [`
    .coa-dd { display: grid; gap: 16px; }
    .coa-dd-head { display: grid; gap: 6px; }
    .coa-dd-name { font-size: 16px; font-weight: 600; }
    .coa-dd-code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; color: var(--erp-color-text-secondary); }
    .coa-dd-name .coa-dd-code { font-size: 13px; margin-inline-end: 6px; }
    .coa-dd-tags { display: flex; flex-wrap: wrap; gap: 4px; }
    .coa-dd-tags:empty { display: none; }
    .coa-dd-tag { font-size: 11px; line-height: 16px; padding: 0 6px; border-radius: 4px; background: var(--erp-color-bg-sunken); color: var(--erp-color-text-secondary); border: 1px solid var(--erp-color-border-subtle); }
    .coa-dd-tag.is-info { background: var(--erp-color-info-bg); border-color: var(--erp-color-info-border); }
    .coa-dd-tag.is-system { background: var(--erp-color-warning-bg); border-color: var(--erp-color-warning-border); color: var(--erp-color-warning-text); }
    .coa-dd-tag.is-off { background: var(--erp-color-error-bg); border-color: var(--erp-color-error-border); color: var(--erp-color-error-text); }
    .coa-dd-actions { display: flex; flex-wrap: wrap; gap: 6px; }
    .coa-dd-fields { display: grid; grid-template-columns: max-content 1fr; gap: 10px 16px; margin: 0; padding-top: 12px; border-top: 1px solid var(--erp-color-border-subtle); }
    .coa-dd-fields dt { color: var(--erp-color-text-secondary); }
    .coa-dd-fields dd { margin: 0; overflow-wrap: anywhere; }
    .coa-dd-status { color: var(--erp-color-success-text, inherit); }
    .coa-dd-status.is-off { color: var(--erp-color-error-text); }
    .coa-dd-balance { font-variant-numeric: tabular-nums; }
    .coa-dd-balance.is-negative { color: var(--erp-color-error-text); }
  `],
})
export class AccountDetailDrawerComponent {
  private readonly store = inject(AccountTreeStore);
  private readonly i18n = inject(I18nService);
  /** the leaf account to show; null = closed */
  readonly account = input<AccountNodeDto | null>(null);
  /** translated sub-category label of the account (the panels know the derived sub-category names) */
  readonly subcategory = input<string | null>(null);
  readonly typeLabel = input.required<(type: string) => string>();
  readonly closed = output<void>();
  readonly edit = output<AccountNodeDto>();
  readonly addSub = output<AccountNodeDto>();
  readonly toggleActive = output<AccountNodeDto>();

  /** "code · name" of the parent: from the open columns when it is there, otherwise fetched once (e.g. a parent of another type). */
  private readonly fetchedParent = signal<{ id: string; label: string } | null>(null);
  readonly parentLabel = computed(() => {
    const id = this.account()?.parentId;
    if (!id) return null;
    const local = this.localParent(id);
    if (local) return local;
    const f = this.fetchedParent();
    return f?.id === id ? f.label : null;
  });

  constructor() {
    effect(() => {
      const id = this.account()?.parentId;
      if (!id) return;
      untracked(() => {
        if (this.localParent(id) || this.fetchedParent()?.id === id) return;
        this.store.account(id).subscribe({ next: (p) => this.fetchedParent.set({ id, label: `${p.code} · ${p.name}` }), error: () => undefined });
      });
    });
  }

  yesNo(v: boolean): string { return v ? this.i18n.t('common.yes', 'Yes') : this.i18n.t('common.no', 'No'); }
  money(v: number): string { return this.i18n.number(v, { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  private localParent(id: string): string | null {
    for (const p of this.store.panels()) {
      const hit = p.parent?.id === id ? p.parent : p.items.find((a) => a.id === id) ?? (p.pin?.id === id ? p.pin : undefined);
      if (hit) return `${hit.code} · ${hit.name}`;
    }
    return null;
  }
}
