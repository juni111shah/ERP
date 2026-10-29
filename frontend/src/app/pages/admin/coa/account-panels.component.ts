import { ChangeDetectionStrategy, Component, DestroyRef, computed, ElementRef, afterNextRender, effect, inject, output, untracked, viewChild, Injector } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AccountCategoryDto, AccountNodeDto, AccountSubcategoryDto } from '../../../core/api/admin.api';
import { asProblem } from '../../../core/http/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../../shared/has-permission.directive';
import { ExpandSearchComponent } from '../../../shared/expand-search.component';
import { RevealDirective } from '../../../shared/reveal.directive';
import { AccountDetailDrawerComponent } from './account-detail-drawer.component';
import { AccountTreeStore, CoaCrumb, CoaPanel } from './account-tree.store';

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
  template: `
    <nav class="coa-crumbs" [attr.aria-label]="'coa.breadcrumb' | translate: 'Account path'" data-testid="coa-breadcrumb">
      @for (c of store.crumbs(); track $index; let last = $last) {
        @if (!last) {
          <button type="button" class="coa-crumb" (click)="store.goTo(c.target)">{{ crumbLabel(c) }}</button>
          <span class="coa-crumb-sep" nz-icon [nzType]="chevron()" aria-hidden="true"></span>
        } @else {
          <span class="coa-crumb is-current" aria-current="page">{{ crumbLabel(c) }}</span>
        }
      }
    </nav>

    <div class="coa-columns" #columns>
      @for (p of store.panels(); track p.key; let i = $index) {
        <section class="coa-panel" [attr.data-testid]="'coa-panel-' + i" [attr.data-kind]="p.kind" [attr.aria-label]="panelTitle(p)" [attr.aria-busy]="p.loading">
          <header class="coa-panel-head">
            <div class="coa-panel-title">
              @if (p.kind === 'categories') { <span nz-icon nzType="partition"></span> }
              @else if (p.sub) { <span nz-icon nzType="folder-open"></span> }
              @else if (p.type) { <span nz-icon [nzType]="typeIcon(p.type)"></span> }
              @else { <span nz-icon nzType="folder-open"></span> }
              <span class="coa-title-text">{{ panelTitle(p) }}</span>
              @if (p.kind === 'accounts' && !p.loading) { <span class="coa-count" data-testid="coa-count">{{ p.total }}</span> }
            </div>
            @if (p.kind === 'accounts') {
              <button *erpHasPermission="'finance.accounts.write'" nz-button nzSize="small" nzType="link" (click)="add(p)" [attr.data-testid]="'coa-add-' + i"
                      [attr.aria-label]="(p.parent ? 'coa.addSub' : 'coa.addAccount') | translate: (p.parent ? 'Add sub-account' : 'Add account')"
                      [title]="(p.parent ? 'coa.addSub' : 'coa.addAccount') | translate: (p.parent ? 'Add sub-account' : 'Add account')">
                <span nz-icon nzType="plus"></span>{{ (p.parent ? 'coa.addSub' : 'coa.addAccount') | translate: (p.parent ? 'Add sub-account' : 'Add account') }}
              </button>
            }
          </header>

          @if (p.kind === 'accounts' && p.parent) {
            <div class="coa-owner">
              <div class="coa-owner-name"><span class="coa-code">{{ p.parent.code }}</span> {{ p.parent.name }}</div>
              <ng-container *ngTemplateOutlet="tags; context: { $implicit: p.parent }" />
              <ng-container *ngTemplateOutlet="actions; context: { $implicit: p.parent, index: i }" />
            </div>
          }

          <div class="coa-search">
            <erp-expand-search size="small" width="100%" [value]="p.search" (valueChange)="onSearch(p.key, $event)" [testId]="'coa-search-' + i"
                               [placeholder]="searchKey(p) | translate: searchFallback(p)"
                               [label]="searchKey(p) | translate: searchFallback(p)" />
          </div>

          <div class="coa-panel-body">
            @if (p.loading) {
              <div class="coa-skeleton" data-testid="coa-skeleton" aria-hidden="true">
                @for (n of skeletonRows; track n) { <div class="coa-skel-row"><span class="coa-skel a"></span><span class="coa-skel b"></span></div> }
              </div>
            } @else if (p.error) {
              <div class="coa-empty">
                <p>{{ 'coa.loadFailed' | translate: 'Could not load this level.' }}</p>
                <button nz-button nzSize="small" (click)="store.retry(p.key)"><span nz-icon nzType="reload"></span>{{ 'coa.retry' | translate: 'Retry' }}</button>
              </div>
            } @else if (p.kind === 'categories') {
              <ul class="coa-list" role="list">
                @for (c of visibleCategories(p); track c.type) {
                  <li>
                    <button type="button" class="coa-item" [class.is-selected]="p.selected === c.type" [attr.aria-current]="p.selected === c.type ? 'true' : null"
                            (click)="store.selectCategory(c.type)" [attr.data-testid]="'coa-cat-' + c.type">
                      <span class="coa-cat-icon" [attr.data-type]="c.type"><span nz-icon [nzType]="typeIcon(c.type)"></span></span>
                      <span class="coa-item-main">
                        <span class="coa-item-name">{{ typeLabel(c.type) }}</span>
                        <span class="coa-item-sub">{{ c.accountCount }} {{ 'coa.accounts' | translate: 'accounts' }} · {{ c.primaryCount }} {{ 'coa.primary' | translate: 'primary' }}</span>
                      </span>
                      @if (c.balance != null) { <span class="coa-balance" [class.is-negative]="c.balance < 0">{{ money(c.balance) }}</span> }
                      <span class="coa-chevron" nz-icon [nzType]="chevron()"></span>
                    </button>
                  </li>
                } @empty { <li class="coa-empty">{{ 'coa.noMatch' | translate: 'Nothing matches.' }}</li> }
              </ul>
            } @else if (p.kind === 'subcategories') {
              <ul class="coa-list" role="list">
                @for (sc of visibleSubcategories(p); track sc.key) {
                  <li>
                    <button type="button" class="coa-item" [class.is-selected]="p.selected === sc.key" [attr.aria-current]="p.selected === sc.key ? 'true' : null"
                            (click)="store.selectSubcategory(i, sc)" [attr.data-testid]="'coa-sub-' + sc.key">
                      <span class="coa-cat-icon"><span nz-icon nzType="folder-open"></span></span>
                      <span class="coa-item-main">
                        <span class="coa-item-name">{{ subLabel(sc.type, sc.key, sc.name) }}</span>
                        <span class="coa-item-sub">{{ sc.accountCount }} {{ 'coa.accounts' | translate: 'accounts' }} · {{ sc.primaryCount }} {{ 'coa.primary' | translate: 'primary' }}</span>
                      </span>
                      @if (sc.balance != null) { <span class="coa-balance" [class.is-negative]="sc.balance < 0">{{ money(sc.balance) }}</span> }
                      <span class="coa-chevron" nz-icon [nzType]="chevron()"></span>
                    </button>
                  </li>
                } @empty {
                  <li class="coa-empty" data-testid="coa-empty">{{ (p.search ? 'coa.noMatch' : 'coa.noSubcategories') | translate: (p.search ? 'Nothing matches.' : 'No sub-categories here yet.') }}</li>
                }
              </ul>
            } @else if (p.kind === 'accounts') {
              <ul class="coa-list" role="list">
                @for (a of p.items; track a.id) {
                  <li>
                    <button type="button" class="coa-item" [class.is-selected]="p.selected === a.id" [class.is-inactive]="!a.isActive" [attr.aria-current]="p.selected === a.id ? 'true' : null"
                            (click)="store.selectAccount(i, a)" (dblclick)="onEdit(a)" [attr.data-testid]="'coa-acct-' + a.code">
                      <span class="coa-code">{{ a.code }}</span>
                      <span class="coa-item-main">
                        <span class="coa-item-name">@if (a.hasChildren) { <span class="coa-group-icon" nz-icon nzType="folder-open" [attr.aria-label]="'coa.tag.group' | translate: 'Group'"></span> }{{ a.name }}</span>
                        <ng-container *ngTemplateOutlet="tags; context: { $implicit: a }" />
                      </span>
                      @if (a.balance != null) { <span class="coa-balance" [class.is-negative]="a.balance < 0">{{ money(a.balance) }}</span> }
                      @if (a.hasChildren) { <span class="coa-kids" [title]="'coa.subAccounts' | translate: 'Sub-accounts'">{{ a.childCount }}</span><span class="coa-chevron" nz-icon [nzType]="chevron()"></span> }
                    </button>
                  </li>
                } @empty {
                  <li class="coa-empty" data-testid="coa-empty">{{ (p.search ? 'coa.noMatch' : 'coa.noAccounts') | translate: (p.search ? 'Nothing matches.' : 'No accounts here yet.') }}</li>
                }
              </ul>
              @if (p.items.length < p.total) {
                <div class="coa-more">
                  <button nz-button nzSize="small" nzBlock [nzLoading]="p.loadingMore" (click)="store.loadMore(p.key)" [attr.data-testid]="'coa-more-' + i">
                    {{ 'coa.loadMore' | translate: 'Load more' }} ({{ p.items.length }} / {{ p.total }})
                  </button>
                </div>
              }
            }
          </div>
        </section>
      }
    </div>

    <erp-account-detail-drawer [account]="store.detail()" [subcategory]="detailSubcategory()" [typeLabel]="typeLabelFn"
                               (closed)="store.closeDetail()" (edit)="onEdit($event)" (addSub)="addSubFromDetail($event)" (toggleActive)="toggleActive($event)" />

    <ng-template #tags let-a>
      <span class="coa-tags">
        @if (!a.isPostable) { <span class="coa-tag">{{ 'coa.tag.header' | translate: 'Header' }}</span> }
        @if (a.isBank) { <span class="coa-tag is-info">{{ 'coa.tag.bank' | translate: 'Bank' }}</span> }
        @if (a.isSystem) { <span class="coa-tag is-system">{{ 'coa.tag.system' | translate: 'System' }}</span> }
        @if (!a.isActive) { <span class="coa-tag is-off">{{ 'coa.tag.inactive' | translate: 'Inactive' }}</span> }
      </span>
    </ng-template>

    <ng-template #actions let-a let-index="index" let-withSub="withSub">
      <div class="coa-actions" erpReveal *erpHasPermission="'finance.accounts.write'">
        <button nz-button nzSize="small" (click)="onEdit(a)" data-testid="coa-edit"
                [attr.aria-label]="'common.edit' | translate: 'Edit'" [title]="'common.edit' | translate: 'Edit'"><span nz-icon nzType="edit"></span>{{ 'common.edit' | translate: 'Edit' }}</button>
        @if (withSub) {
          <button nz-button nzSize="small" (click)="create.emit({ type: a.type, parent: a })" data-testid="coa-add-sub"
                  [attr.aria-label]="'coa.addSub' | translate: 'Add sub-account'" [title]="'coa.addSub' | translate: 'Add sub-account'"><span nz-icon nzType="plus"></span>{{ 'coa.addSub' | translate: 'Add sub-account' }}</button>
        }
        @if (!a.isSystem) {
          <button nz-button nzSize="small" [nzDanger]="a.isActive" nz-popconfirm
                  [nzPopconfirmTitle]="(a.isActive ? 'coa.confirmDeactivate' : 'coa.confirmActivate') | translate: (a.isActive ? 'Deactivate this account? It stays in history but cannot be used on new entries.' : 'Activate this account again?')"
                  (nzOnConfirm)="toggleActive(a)" data-testid="coa-toggle-active"
                  [attr.aria-label]="(a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate')"
                  [title]="(a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate')">
            <span nz-icon [nzType]="a.isActive ? 'stop' : 'check-circle'"></span>{{ (a.isActive ? 'coa.deactivate' : 'coa.activate') | translate: (a.isActive ? 'Deactivate' : 'Activate') }}
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: [`
    :host { display: block; }
    .coa-crumbs { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; margin-bottom: 12px; min-height: 24px; font-size: 13px; }
    .coa-crumb { border: 0; background: none; padding: 2px 4px; border-radius: 4px; color: var(--erp-color-primary-text); cursor: pointer; font: inherit; }
    .coa-crumb:hover { background: var(--erp-color-primary-subtle); }
    .coa-crumb:focus-visible { outline: none; box-shadow: var(--erp-focus-ring); }
    .coa-crumb.is-current { color: var(--erp-color-text-primary); font-weight: 600; cursor: default; }
    .coa-crumb-sep { color: var(--erp-color-text-muted); font-size: 10px; }

    .coa-columns { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 8px; scroll-snap-type: x proximity; align-items: stretch; }
    .coa-panel {
      flex: 0 0 336px; min-width: 0; display: flex; flex-direction: column; height: calc(100vh - 230px); min-height: 380px;
      background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: var(--erp-radius-base);
      scroll-snap-align: start; animation: coa-in 220ms ease-out both;
    }
    .coa-panel:first-child { animation: none; }
    @keyframes coa-in { from { opacity: 0; transform: translateX(24px); } to { opacity: 1; transform: none; } }
    @keyframes coa-in-rtl { from { opacity: 0; transform: translateX(-24px); } to { opacity: 1; transform: none; } }
    :host-context([dir='rtl']) .coa-panel { animation-name: coa-in-rtl; }

    .coa-panel-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 10px 12px; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .coa-panel-title { display: flex; align-items: center; gap: 8px; font-weight: 600; min-width: 0; }
    .coa-title-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .coa-count { font-weight: 500; font-size: 12px; color: var(--erp-color-text-secondary); background: var(--erp-color-bg-sunken); border-radius: 10px; padding: 0 8px; }
    .coa-owner { padding: 10px 12px; border-bottom: 1px solid var(--erp-color-border-subtle); background: var(--erp-color-bg-sunken); display: grid; gap: 6px; }
    .coa-owner-name { font-weight: 600; }
    .coa-search { padding: 8px 12px; }
    .coa-panel-body { flex: 1; overflow-y: auto; min-height: 0; }

    .coa-list { list-style: none; margin: 0; padding: 4px 0; }
    .coa-item {
      display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 12px; border: 0; border-inline-start: 3px solid transparent;
      background: none; color: inherit; font: inherit; text-align: start; cursor: pointer;
    }
    .coa-item:hover { background: var(--erp-color-nav-item-hover-bg); }
    .coa-item:focus-visible { outline: none; box-shadow: inset var(--erp-focus-ring); }
    .coa-item.is-selected { background: var(--erp-color-primary-subtle); border-inline-start-color: var(--erp-color-primary); }
    .coa-item.is-inactive .coa-item-name { color: var(--erp-color-text-muted); text-decoration: line-through; }
    .coa-item-main { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .coa-item-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .coa-item-sub { font-size: 12px; color: var(--erp-color-text-secondary); }
    .coa-group-icon { color: var(--erp-color-primary-text); margin-inline-end: 6px; font-size: 13px; }
    .coa-code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--erp-color-text-secondary); min-width: 44px; }
    .coa-cat-icon { display: inline-flex; align-items: center; justify-content: center; width: 30px; height: 30px; border-radius: 8px; background: var(--erp-color-primary-subtle); color: var(--erp-color-primary-text); }
    .coa-balance { font-variant-numeric: tabular-nums; font-size: 12px; white-space: nowrap; color: var(--erp-color-text-primary); }
    .coa-balance.is-negative { color: var(--erp-color-error-text); }
    .coa-kids { font-size: 11px; color: var(--erp-color-text-secondary); background: var(--erp-color-bg-sunken); border-radius: 10px; padding: 0 6px; }
    .coa-chevron { color: var(--erp-color-text-muted); font-size: 11px; }
    .coa-tags { display: flex; flex-wrap: wrap; gap: 4px; }
    .coa-tags:empty { display: none; }
    .coa-tag { font-size: 11px; line-height: 16px; padding: 0 6px; border-radius: 4px; background: var(--erp-color-bg-sunken); color: var(--erp-color-text-secondary); border: 1px solid var(--erp-color-border-subtle); }
    .coa-tag.is-info { background: var(--erp-color-info-bg); border-color: var(--erp-color-info-border); }
    .coa-tag.is-system { background: var(--erp-color-warning-bg); border-color: var(--erp-color-warning-border); color: var(--erp-color-warning-text); }
    .coa-tag.is-off { background: var(--erp-color-error-bg); border-color: var(--erp-color-error-border); color: var(--erp-color-error-text); }
    .coa-actions { display: flex; flex-wrap: wrap; gap: 6px; }
    .coa-more { padding: 8px 12px; }
    .coa-empty { padding: 24px 12px; text-align: center; color: var(--erp-color-text-secondary); list-style: none; }

    .coa-skeleton { padding: 8px 12px; display: grid; gap: 14px; }
    .coa-skel-row { display: flex; gap: 10px; align-items: center; }
    .coa-skel { display: block; height: 12px; border-radius: 4px; background: linear-gradient(90deg, var(--erp-color-bg-sunken) 25%, var(--erp-color-border-subtle) 50%, var(--erp-color-bg-sunken) 75%); background-size: 200% 100%; animation: coa-shimmer 1.2s linear infinite; }
    .coa-skel.a { width: 44px; } .coa-skel.b { flex: 1; }
    @keyframes coa-shimmer { from { background-position: 200% 0; } to { background-position: -200% 0; } }

    @media (prefers-reduced-motion: reduce) {
      .coa-panel, .coa-skel { animation: none !important; }
    }
    /* Phones: the columns stack, newest at the bottom. */
    @media (max-width: 767px) {
      .coa-columns { flex-direction: column; overflow-x: visible; scroll-snap-type: none; }
      .coa-panel { flex: none; width: 100%; height: auto; min-height: 0; max-height: 70vh; }
    }
  `],
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
      next: () => this.message.success(a.isActive ? this.i18n.t('coa.deactivated', 'Account deactivated') : this.i18n.t('coa.activated', 'Account activated')),
      error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? this.i18n.t('coa.saveFailed', 'Could not update the account')); },
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
