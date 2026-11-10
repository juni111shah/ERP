import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AccountNodeDto } from '@core/api/admin.api';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { AccountTreeStore } from '../../account-tree.store';

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
  templateUrl: './account-detail-drawer.component.html',
  styleUrl: './account-detail-drawer.component.scss',
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
