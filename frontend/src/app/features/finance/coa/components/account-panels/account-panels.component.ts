import { ChangeDetectionStrategy, Component, DestroyRef, computed, ElementRef, afterNextRender, effect, inject, output, untracked, viewChild, Injector } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AccountCategoryDto, AccountNodeDto, AccountSubcategoryDto } from '@core/api/admin.api';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ExpandSearchComponent } from '@shared/components/expand-search/expand-search.component';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { AccountDetailDrawerComponent } from '../account-detail-drawer/account-detail-drawer.component';
import { AccountTreeStore, CoaCrumb, CoaPanel } from '../../account-tree.store';

export interface CoaCreateRequest { type: string; parent: AccountNodeDto | null; /** reporting group a new account in a sub-category starts with */ reportingGroup?: string | null; }

const TYPE_ICONS: Record<string, string> = { asset: 'wallet', liability: 'credit-card', equity: 'fund', income: 'rise', expense: 'fall' };
const TYPE_LABELS: Record<string, string> = { asset: 'Assets', liability: 'Liabilities', equity: 'Equity', income: 'Revenue', expense: 'Expenses' };
/** English fallbacks of the derived sub-categories (the server sends the same names; keys are unique within a type). */
const SUB_LABELS: Record<string, string> = {
  'asset.current': 'Current assets', 'asset.noncurrent': 'Non-current assets',
  'liability.current': 'Current liabilities', 'liability.noncurrent': 'Non-current liabilities',
  'equity.equity': 'Equity', 'income.operating': 'Operating revenue', 'income.other': 'Other income',
  'expense.costOfSales': 'Cost of sales', 'expense.operating': 'Operating expenses', 'expense.other': 'Other expenses',
};

/**
 * Step-by-step chart of accounts (Miller columns). Column 1 lists the account categories; choosing one opens column 2 with
 * that category's sub-categories (Current assets, Non-current assets, …); choosing one opens column 3 with its primary
 * accounts; choosing an account with sub-accounts opens its children, and so on. A leaf account is selected in place and
 * its details open in a side drawer (closing it keeps the columns). Every column is fetched only when it opens (see AccountTreeStore), one path is open at a time.
 */
@Component({
  selector: 'erp-account-panels',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, NzButtonModule, NzIconModule, NzPopconfirmModule, TranslatePipe, HasPermissionDirective, RevealDirective, ExpandSearchComponent, AccountDetailDrawerComponent],
  templateUrl: './account-panels.component.html',
  styleUrl: './account-panels.component.scss',
})
export class AccountPanelsComponent {
  readonly store = inject(AccountTreeStore);
  private readonly i18n = inject(I18nService);
  private readonly message = inject(NzMessageService);
  private readonly injector = inject(Injector);
  private readonly columns = viewChild<ElementRef<HTMLElement>>('columns');
  readonly create = output<CoaCreateRequest>();
  readonly edit = output<AccountNodeDto>();
  readonly skeletonRows = [1, 2, 3, 4, 5, 6];
  /** points to the next column: right in LTR, left in RTL */
  readonly chevron = computed(() => (this.i18n.dir() === 'rtl' ? 'left' : 'right'));
  private readonly timers = new Map<string, ReturnType<typeof setTimeout>>();

  constructor() {
    this.store.init();
    inject(DestroyRef).onDestroy(() => { this.timers.forEach((t) => clearTimeout(t)); this.store.closeDetail(); });
    // Bring the newest column into view when one opens (horizontal scroll on narrow screens, vertical when stacked).
    let count = 0;
    effect(() => {
      const n = this.store.panels().length;
      if (n > count && count > 0) untracked(() => afterNextRender(() => this.revealLast(), { injector: this.injector }));
      count = n;
    });
  }

  /** Translated sub-category of the account in the details drawer (its own key, or the sub-category column it was opened from). */
  readonly detailSubcategory = computed(() => {
    const d = this.store.detail();
    if (!d) return null;
    const key = d.subcategory ?? this.store.panels().find((p) => p.sub && p.type === d.type)?.sub?.key;
    if (!key) return null;
    const name = this.store.panels().find((p) => p.sub?.key === key && p.type === d.type)?.sub?.name;
    return this.subLabel(d.type, key, name);
  });
  readonly typeLabelFn = (type: string): string => this.typeLabel(type);

  /** Edit from a row, the owner card or the details drawer; the drawer gives way to the form and returns if it is cancelled. */
  onEdit(a: AccountNodeDto): void {
    if (this.store.detail()?.id === a.id) this.store.suspendDetail();
    this.edit.emit(a);
  }

  addSubFromDetail(a: AccountNodeDto): void {
    this.store.suspendDetail();
    this.create.emit({ type: a.type, parent: a });
  }

  panelTitle(p: CoaPanel): string {
    if (p.kind === 'categories') return this.i18n.t('coa.categories', 'Account categories');
    if (p.sub && p.type) return this.subLabel(p.type, p.sub.key, p.sub.name);
    if (p.type) return this.typeLabel(p.type);
    return p.parent?.name ?? '';
  }

  crumbLabel(c: CoaCrumb): string {
    if (c.kind === 'root') return this.i18n.t('coa.title', 'Chart of accounts');
    if (c.kind === 'type') return this.typeLabel(c.label);
    if (c.kind === 'sub') return this.subLabel(c.type ?? '', c.code ?? '', c.label);
    return c.code ? `${c.code} · ${c.label}` : c.label;
  }

  /** Translated sub-category name; the server's English name (or the built-in one) is the fallback. */
  subLabel(type: string, key: string, name?: string | null): string {
    const id = `${type}.${key}`;
    const fallback = name && name !== key ? name : SUB_LABELS[id] ?? key;
    return this.i18n.t(`coa.sub.${id}`, fallback);
  }

  searchKey(p: CoaPanel): string {
    return p.kind === 'categories' ? 'coa.searchCategories' : p.kind === 'subcategories' ? 'coa.searchSubcategories' : 'coa.searchAccounts';
  }

  searchFallback(p: CoaPanel): string {
    return p.kind === 'categories' ? 'Search categories' : p.kind === 'subcategories' ? 'Search sub-categories' : 'Search code or name';
  }

  visibleSubcategories(p: CoaPanel): AccountSubcategoryDto[] {
    const term = p.search.trim().toLowerCase();
    return term ? p.subcategories.filter((s) => this.subLabel(s.type, s.key, s.name).toLowerCase().includes(term) || s.name.toLowerCase().includes(term)) : p.subcategories;
  }

  typeLabel(type: string): string { return this.i18n.t(`coa.type.${type}`, TYPE_LABELS[type] ?? type); }
  typeIcon(type: string): string { return TYPE_ICONS[type] ?? 'account-book'; }
  yesNo(v: boolean): string { return v ? this.i18n.t('common.yes', 'Yes') : this.i18n.t('common.no', 'No'); }
  money(v: number): string { return this.i18n.number(v, { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  visibleCategories(p: CoaPanel): AccountCategoryDto[] {
    const term = p.search.trim().toLowerCase();
    return term ? p.categories.filter((c) => this.typeLabel(c.type).toLowerCase().includes(term) || c.type.includes(term)) : p.categories;
  }

  add(p: CoaPanel): void {
    if (p.type) this.create.emit({ type: p.type, parent: null, reportingGroup: p.sub ? p.sub.name : null });
    else if (p.parent) this.create.emit({ type: p.parent.type, parent: p.parent });
  }

  onSearch(key: string, term: string): void {
    clearTimeout(this.timers.get(key));
    if (key === 'root' || key.startsWith('type:')) { this.store.search(key, term); return; } // categories and sub-categories: filtered in the browser
    this.timers.set(key, setTimeout(() => this.store.search(key, term.trim()), 300));
  }

  toggleActive(a: AccountNodeDto): void {
    this.store.setActive(a.id, !a.isActive).subscribe({
      next: () => this.message.success(`${a.isActive ? this.i18n.t('coa.deactivated', 'Account deactivated') : this.i18n.t('coa.activated', 'Account activated')}: ${a.code} ${a.name}`),
      error: (e) => { this.message.error(problemMessage(e, this.i18n.t('coa.saveFailed', 'Could not update the account'))); },
    });
  }

  private revealLast(): void {
    const host = this.columns()?.nativeElement;
    const last = host?.lastElementChild as HTMLElement | null | undefined;
    if (!last) return;
    const reduced = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    last.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'nearest', inline: 'nearest' });
  }
}
