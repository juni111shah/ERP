import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { forkJoin } from 'rxjs';
import { CountryDto, CurrencyOption } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { BillingCycle, ClientRow, ClientStartMode, IndustryOption, PlanView, PlatformApi } from '../../core/api/platform.api';
import { suggestWorkspaceCode } from '../../core/format/subscription';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { ErpDatePipe, ErpMoneyPipe } from '../../shared/display.pipes';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { CYCLE_OPTIONS, OneTimePasswordComponent, OneTimeSecret, PLATFORM_STYLES, START_OPTIONS, STATUS_FILTERS, cycleWords, planOptions, statusWords } from './platform-ui';

/** Everyone who uses the ERP, how they stand, and a way to add one by hand. */
@Component({
  selector: 'erp-platform-clients',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzIconModule, NzSelectModule, NzToolTipModule, ErpDatePipe, ErpMoneyPipe, OneTimePasswordComponent, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .filters { display: inline-flex; flex-wrap: wrap; gap: 8px; }
    .filters nz-select { width: 170px; }
    .marker { color: var(--erp-color-warning-text); margin-inline-start: 6px; }
    @media (max-width: 767px) { .filters { width: 100%; } .filters nz-select { flex: 1 1 140px; width: auto; } }
  `],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search by name or code" emptyText="No clients yet" [filterable]="false" [configurable]="false" [exportable]="false"
                     tableName="clients" scrollX="1080px" (rowClick)="open($event)">
      <div toolbar class="filters">
        <nz-select [ngModel]="status()" (ngModelChange)="setStatus($event)" nzAllowClear nzPlaceHolder="Any status" data-reveal="off" data-testid="clients-status-filter">
          @for (s of statusFilters; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
        </nz-select>
        <nz-select [ngModel]="planId()" (ngModelChange)="setPlan($event)" nzAllowClear nzPlaceHolder="Any plan" data-reveal="off" data-testid="clients-plan-filter">
          @for (p of plans(); track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.name"></nz-option> }
        </nz-select>
        <button nz-button nzType="primary" type="button" (click)="newClient()" [nzLoading]="preparing()" data-testid="clients-new"><span nz-icon nzType="plus"></span>New client</button>
      </div>
      <ng-template #cell let-r let-col="column">
        @switch (col.key) {
          @case ('name') {
            <a [routerLink]="['/platform/clients', r.orgId]" (click)="$event.stopPropagation()">{{ r.name }}</a>
            @if (r.hasPlanRequest) { <span class="marker" nz-icon nzType="swap" nz-tooltip nzTooltipTitle="A plan change is requested" aria-label="A plan change is requested" [attr.data-testid]="'plan-request-' + r.code"></span> }
          }
          @case ('code') { <span class="pf-mono">{{ r.code }}</span> }
          @case ('plan') { {{ r.planName ?? '—' }}@if (r.isComplimentary) { <span class="pf-muted pf-small"> (free)</span> } }
          @case ('status') { <erp-status [value]="r.status ?? 'inactive'" [label]="statusText(r.status)" /> }
          @case ('price') { @if (r.cyclePrice !== null) { <span class="pf-nowrap">{{ r.cyclePrice | erpMoney: currency() }}</span><span class="pf-muted pf-small">, {{ cycleText(r.cycle) }}</span> } @else { — } }
          @case ('users') { <span class="pf-nowrap">{{ r.activeUsers }}{{ r.maxUsers ? ' of ' + r.maxUsers : '' }}</span> }
          @case ('lastSignInAt') { <span class="pf-nowrap">{{ r.lastSignInAt ? (r.lastSignInAt | erpDate) : 'Never' }}</span> }
          @case ('openBalance') { <span class="pf-nowrap">{{ r.openBalance | erpMoney: currency() }}</span> }
        }
      </ng-template>
    </erp-paged-table>

    <erp-record-drawer title="New client" [open]="drawerOpen()" [fields]="fields()" [saving]="saving()" [errors]="errors()" submitLabel="Create client" [width]="720" testId="client-form"
                       (submitted)="save($event)" (closed)="drawerOpen.set(false)" />
    <erp-one-time-password [secret]="secret()" actionLabel="Open the client" (closed)="afterCreated()" />`,
})
export class PlatformClientsPage {
  private readonly api = inject(PlatformApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);

  readonly statusFilters = STATUS_FILTERS;
  readonly status = signal<string | null>(this.route.snapshot.queryParamMap.get('status'));
  readonly planId = signal<string | null>(this.route.snapshot.queryParamMap.get('planId'));
  readonly plans = signal<PlanView[]>([]);
  /** Prices come in the platform's billing currency, which the plans carry. */
  readonly currency = computed(() => this.plans()[0]?.currency ?? '');
  readonly list = createPagedList<ClientRow>((q) => this.api.clients(q), { extra: this.extra(), pageSize: 25 });
  readonly columns: TableColumn<ClientRow>[] = [
    { key: 'name', label: 'Client', width: '160px', value: (r) => r.name },
    { key: 'code', label: 'Workspace code', width: '140px', value: (r) => r.code },
    { key: 'plan', label: 'Plan', width: '120px', value: (r) => r.planName },
    { key: 'status', label: 'Status', width: '140px', value: (r) => statusWords(r.status) },
    { key: 'price', label: 'Price per cycle', width: '170px', value: (r) => r.cyclePrice },
    { key: 'users', label: 'Users', width: '80px', value: (r) => r.activeUsers },
    { key: 'lastSignInAt', label: 'Last sign-in', width: '150px', value: (r) => r.lastSignInAt },
    { key: 'openBalance', label: 'Balance', width: '120px', align: 'right', value: (r) => r.openBalance },
  ];

  // ---- New client ------------------------------------------------------------------------------------------------------
  private readonly industries = signal<IndustryOption[]>([]);
  private readonly countries = signal<CountryDto[]>([]);
  private readonly currencies = signal<CurrencyOption[]>([]);
  readonly drawerOpen = signal(false);
  readonly preparing = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly secret = signal<OneTimeSecret | null>(null);
  private createdId: string | null = null;

  readonly fields = computed<FieldSpec[]>(() => {
    const countries = this.countries();
    return [
      { key: 'workspaceName', label: 'Business name', type: 'text', required: true, maxLength: 200, onChange: (v, form, prev) => {
        const typed = form['signInKey'] as string | null;
        return !typed || typed === suggestWorkspaceCode(prev['workspaceName'] as string) ? { signInKey: suggestWorkspaceCode(v as string) } : null;
      } },
      { key: 'signInKey', label: 'Workspace code', type: 'text', required: true, maxLength: 49, hint: 'What they use to reach this workspace. Letters, digits and dashes.' },
      { key: 'industry', label: 'Industry', type: 'select', required: true, searchable: true, options: this.industries().filter((i) => (i.availability ?? '').toLowerCase() === 'available').map((i) => ({ value: i.key, label: i.label })) },
      { key: 'countryCode', label: 'Country', type: 'select', required: true, searchable: true, options: countries.map((c) => ({ value: c.code, label: c.name })), onChange: (v, form, prev) => {
        const next = countries.find((c) => c.code === v)?.defaultCurrency;
        const before = countries.find((c) => c.code === prev['countryCode'])?.defaultCurrency;
        return next && (!form['baseCurrency'] || form['baseCurrency'] === before) ? { baseCurrency: next } : null;
      } },
      { key: 'baseCurrency', label: 'Currency', type: 'select', required: true, searchable: true, options: this.currencies().map((c) => ({ value: c.code, label: `${c.code} · ${c.name}` })) },
      { key: 'adminName', label: 'Administrator name', type: 'text', required: true, maxLength: 200 },
      { key: 'adminEmail', label: 'Administrator email', type: 'text', required: true, maxLength: 256 },
      { key: 'phone', label: 'Phone', type: 'text', maxLength: 40 },
      { key: 'planId', label: 'Plan', type: 'select', required: true, options: planOptions(this.plans()) },
      { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, default: 'monthly', options: CYCLE_OPTIONS },
      { key: 'startMode', label: 'How it starts', type: 'select', required: true, default: 'trial', options: START_OPTIONS, hint: 'Starting as active issues the first invoice straight away.' },
      { key: 'trialDays', label: 'Trial days', type: 'number', min: 1, max: 365, precision: 0, hint: 'Leave empty to use the plan\'s trial.', hidden: (v) => v['startMode'] !== 'trial' },
      { key: 'customPrice', label: 'Custom price per cycle', type: 'number', min: 0, hint: 'Leave empty to use the plan\'s price.', hidden: (v) => v['startMode'] === 'complimentary' },
      { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000, hint: 'For your team only.' },
    ];
  });

  constructor() {
    this.api.plans().pipe(takeUntilDestroyed()).subscribe({ next: (p) => this.plans.set(p), error: () => undefined });
  }

  private extra(): Record<string, string> {
    const extra: Record<string, string> = {};
    if (this.status()) extra['status'] = this.status()!;
    if (this.planId()) extra['planId'] = this.planId()!;
    return extra;
  }

  setStatus(value: string | null): void { this.status.set(value); this.list.setExtra(this.extra()); }
  setPlan(value: string | null): void { this.planId.set(value); this.list.setExtra(this.extra()); }
  statusText(status: ClientRow['status']): string { return statusWords(status); }
  cycleText(cycle: BillingCycle | null): string { return cycleWords(cycle).toLowerCase(); }
  open(row: ClientRow): void { void this.router.navigate(['/platform/clients', row.orgId]); }

  newClient(): void {
    this.errors.set(null);
    if (this.countries().length) { this.drawerOpen.set(true); return; }
    this.preparing.set(true);
    forkJoin({ industries: this.api.industries(), countries: this.api.countries(), currencies: this.api.currencies(), plans: this.api.plans() }).subscribe({
      next: (r) => {
        this.industries.set(r.industries); this.countries.set(r.countries); this.currencies.set(r.currencies); this.plans.set(r.plans);
        this.preparing.set(false);
        this.drawerOpen.set(true);
      },
      error: (e) => { this.preparing.set(false); this.message.error(problemMessage(e, 'Could not open the form')); },
    });
  }

  save(v: Record<string, unknown>): void {
    this.saving.set(true);
    this.errors.set(null);
    const startMode = v['startMode'] as ClientStartMode;
    this.api.createClient({
      workspaceName: v['workspaceName'] as string, signInKey: v['signInKey'] as string, industry: v['industry'] as string, countryCode: v['countryCode'] as string, baseCurrency: v['baseCurrency'] as string,
      timeZone: this.countries().find((c) => c.code === v['countryCode'])?.defaultTimeZone ?? null, adminName: v['adminName'] as string, adminEmail: v['adminEmail'] as string, phone: v['phone'] as string | null,
      planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, startMode, trialDays: startMode === 'trial' ? (v['trialDays'] as number | null) ?? null : null,
      customPrice: startMode === 'complimentary' ? null : (v['customPrice'] as number | null) ?? null, notes: v['notes'] as string | null,
    }).subscribe({
      next: (created) => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.message.success('Client created');
        this.createdId = created.orgId;
        this.list.reload();
        this.secret.set({ title: 'Client created', password: created.temporaryPassword, facts: [{ label: 'Workspace code', value: created.signInKey }, { label: 'Administrator', value: created.adminEmail }] });
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors) this.message.error(problemMessage(e, 'Could not create the client'));
      },
    });
  }

  afterCreated(): void {
    this.secret.set(null);
    if (this.createdId) void this.router.navigate(['/platform/clients', this.createdId]);
    this.createdId = null;
  }
}
