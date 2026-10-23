import { ChangeDetectionStrategy, Component, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import { ACCOUNT_TYPES, AccountDto, AccountNodeDto, AccountsApi, CoaNumbering } from '../../core/api/admin.api';
import { asProblem } from '../../core/http/problem-details';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { RevealDirective } from '../../shared/reveal.directive';
import { AccountPanelsComponent, CoaCreateRequest } from './coa/account-panels.component';
import { AccountTreeStore } from './coa/account-tree.store';
import { AccountsListComponent } from './coa/accounts-list.component';

type CoaView = 'panels' | 'list';
const VIEW_KEY = 'erp.coa.view';

/** What the drawer opens with: an existing account, or a new one prefilled from the panel it was started in. */
type AccountDraft = Partial<AccountDto> & { id: string; _parentLabel?: string };

@Component({
  selector: 'erp-accounts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AccountPanelsComponent, AccountsListComponent, RecordDrawerComponent, HasPermissionDirective, FormsModule, NzButtonModule, NzIconModule, NzRadioModule, NzToolTipModule, RevealDirective, TranslatePipe],
  template: `
    <div class="coa-page-head">
      <h1 class="erp-page-title">{{ 'coa.title' | translate: 'Chart of accounts' }}</h1>
      <div class="coa-page-tools" erpReveal>
        <nz-radio-group [ngModel]="view()" (ngModelChange)="setView($event)" nzButtonStyle="solid" nzSize="small" data-testid="coa-view">
          <label nz-radio-button nzValue="panels" data-testid="coa-view-panels"><span nz-icon nzType="partition"></span> {{ 'coa.view.panels' | translate: 'Step by step' }}</label>
          <label nz-radio-button nzValue="list" data-testid="coa-view-list"><span nz-icon nzType="unordered-list"></span> {{ 'coa.view.list' | translate: 'List view' }}</label>
        </nz-radio-group>
        @if (view() === 'panels') {
          <button nz-button nzType="default" type="button" (click)="refreshTree()" data-testid="coa-refresh"><span nz-icon nzType="reload"></span> {{ 'ui.common.refresh' | translate: 'Refresh' }}</button>
          <button *erpHasPermission="'finance.accounts.write'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'coa.newAccount' | translate: 'New account'" nz-tooltip [nzTooltipTitle]="'coa.newAccount' | translate: 'New account'" nzType="primary" (click)="newAccount(null)" data-testid="account-new">
            <span nz-icon nzType="plus"></span>{{ 'coa.newAccount' | translate: 'New account' }}
          </button>
        }
      </div>
    </div>
    @if (view() === 'panels') {
      <erp-account-panels (create)="newAccount($event)" (edit)="editNode($event)" />
    } @else {
      <erp-accounts-list (create)="newAccount(null)" (edit)="editAccount($event)" />
    }
    <erp-record-drawer [title]="state.editing()?.id ? ('coa.editAccount' | translate: 'Edit account') : ('coa.newAccount' | translate: 'New account')" [open]="state.open()" [fields]="fields()"
                       [value]="$any(state.editing())" customFieldEntityType="account"
                       [saving]="state.saving()" [errors]="state.errors()" testId="account-form" (submitted)="state.submit($event)" (closed)="closeForm()" />`,
  styles: [`
    .coa-page-head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
    .coa-page-head .erp-page-title { margin: 0; }
    .coa-page-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
  `],
})
export class AccountsPage {
  private readonly api = inject(AccountsApi);
  private readonly tree = inject(AccountTreeStore);
  private readonly i18n = inject(I18nService);
  private readonly message = inject(NzMessageService);
  private readonly listView = viewChild(AccountsListComponent);
  readonly view = signal<CoaView>(localStorage.getItem(VIEW_KEY) === 'list' ? 'list' : 'panels');
  /** type and parent a new account starts with (from the panel "Add account" / "Add sub-account" buttons) */
  private readonly prefill = signal<CoaCreateRequest | null>(null);
  private savedId: string | null = null;
  /** numbering mask of the workspace; in hierarchical mode a new account's code is suggested and checked against it */
  private readonly numbering = signal<CoaNumbering | null>(null);
  /** the code suggested for the account being created (null = none / free mode) */
  private readonly suggested = signal<string | null>(null);

  readonly state = new MasterDataState<AccountDraft>({
    save: (id, body) => this.persist(id, body as Record<string, unknown>).pipe(tap((saved) => (this.savedId = saved?.id ?? id))),
    reload: () => {
      if (this.view() === 'list') { this.listView()?.reload(); this.tree.invalidate(); }
      else this.tree.refresh(this.savedId);
    },
    message: this.message, entityName: 'Account',
  });

  constructor() { this.api.numbering().subscribe({ next: (n) => this.numbering.set(n), error: () => undefined }); }

  /** Saves; when an automatically suggested code was taken meanwhile (409), asks for the next one and tries once more. */
  private persist(id: string | null, body: Record<string, unknown>) {
    if (id) return this.api.update(id, body);
    return this.api.create(body).pipe(catchError((e) => {
      const suggested = this.suggested();
      if (e?.status !== 409 || !suggested || body['code'] !== suggested) return throwError(() => e);
      return this.api.nextCode((body['parentId'] as string | null) ?? null, (body['type'] as string | null) ?? null).pipe(
        switchMap((n) => (n.code && n.code !== suggested ? this.api.create({ ...body, code: n.code }) : throwError(() => e))),
      );
    }));
  }

  /** Reloads the open columns of the step-by-step view (the list view has its own table Refresh). */
  refreshTree(): void { this.tree.refresh(); }

  readonly fields = computed<FieldSpec[]>(() => {
    const pre = this.prefill();
    const parentOption = pre?.parent ? { value: pre.parent.id, label: `${pre.parent.code} · ${pre.parent.name}` } : null;
    return [
      {
        key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20, default: this.suggested(),
        hint: this.numbering()?.mode === 'hierarchical'
          ? `${this.i18n.t('coa.codeHint', 'Format')} ${this.numbering()!.pattern}${this.suggested() ? ' · ' + this.i18n.t('coa.codeSuggested', 'Suggested from the numbering mask; you can change it.') : ''}`
          : undefined,
      },
      { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
      { key: 'type', label: 'Type', type: 'select', required: true, options: ACCOUNT_TYPES, default: pre?.type ?? 'asset' },
      { key: 'reportingGroup', label: 'Reporting group', type: 'text', maxLength: 60, default: pre?.reportingGroup ?? null },
      {
        key: 'parentId', label: 'Parent account', type: 'lookup', default: parentOption?.value ?? null,
        lookup: (term) => this.api.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => {
          const options = r.items.map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` }));
          // keep the prefilled parent selectable (and labelled) even when it is not in the first suggestions
          return parentOption && !term ? [parentOption, ...options.filter((o) => o.value !== parentOption.value)] : options;
        })),
        currentLabel: (r) => (r['_parentLabel'] as string | undefined) ?? String(r['parentId']),
      },
      { key: 'isPostable', label: 'Postable (not a header)', type: 'switch', default: true },
      { key: 'isBank', label: 'Bank / cash account', type: 'switch', default: false },
      { key: 'currencyCode', label: 'Currency (if not base)', type: 'text', maxLength: 3 },
      { key: 'isActive', label: 'Active', type: 'switch', default: true, hint: 'System accounts keep their code, type and active flag.' },
    ];
  });

  /** Cancelling the form brings back the account details drawer it was opened from (if any). */
  closeForm(): void {
    this.state.close();
    this.tree.resumeDetail();
  }

  setView(view: CoaView): void {
    this.view.set(view);
    localStorage.setItem(VIEW_KEY, view);
  }

  newAccount(request: CoaCreateRequest | null): void {
    this.prefill.set(request);
    this.savedId = null;
    this.suggested.set(null);
    if (this.numbering()?.mode !== 'hierarchical') { this.state.create(); return; }
    // Ask for the next free code before the form opens, so the suggestion is in place when the user starts typing.
    this.api.nextCode(request?.parent?.id ?? null, request?.type ?? null).pipe(catchError((e) => {
      const p = asProblem(e);
      this.message.warning(p?.detail ?? p?.title ?? this.i18n.t('coa.loadFailed', 'Could not load this level.'));
      return of({ code: '' });
    })).subscribe((n) => { this.suggested.set(n.code || null); this.state.create(); });
  }

  editAccount(account: AccountDraft): void {
    this.prefill.set(null);
    this.suggested.set(null);
    this.savedId = null;
    this.state.edit(account);
  }

  /** Panel rows carry no custom fields, so the full account is fetched before the drawer opens. */
  editNode(node: AccountNodeDto): void {
    const parentLabel = this.parentLabel(node);
    this.tree.account(node.id).subscribe({
      next: (account) => this.editAccount({ ...account, _parentLabel: parentLabel } as AccountDto),
      error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? this.i18n.t('coa.loadFailed', 'Could not load this level.')); },
    });
  }

  private parentLabel(node: AccountNodeDto): string | undefined {
    if (!node.parentId) return undefined;
    for (const p of this.tree.panels()) {
      const hit = p.parent?.id === node.parentId ? p.parent : p.items.find((a) => a.id === node.parentId);
      if (hit) return `${hit.code} · ${hit.name}`;
    }
    return undefined;
  }
}
