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
import { CountryDto, CurrencyOption } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { BillingCycle, ClientRow, IndustryOption, PlanView, PlatformApi } from '@core/api/platform.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { OneTimePasswordComponent, OneTimeSecret } from '../../components/one-time-password/one-time-password.component';
import { newClientFields } from '../../platform-client-dialogs';
import { DrawerValue, toCreateClientInput } from '../../platform-forms';
import { PLATFORM_STYLES, STATUS_FILTERS, cycleWords, statusWords } from '../../platform-ui';

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
  templateUrl: './platform-clients.page.html',
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

  readonly fields = computed<FieldSpec[]>(() =>
    newClientFields({ countries: this.countries(), industries: this.industries(), currencies: this.currencies(), plans: this.plans() }));

  constructor() {
    loadInto(this.plans, this.api.plans().pipe(takeUntilDestroyed()));
  }

  /** The list's query parameters: the status and plan filters that are set. */
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

  /** Opens the new-client drawer, loading its pick lists the first time. */
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

  save(v: DrawerValue): void {
    this.saving.set(true);
    this.errors.set(null);
    this.api.createClient(toCreateClientInput(v, this.countries())).subscribe({
      next: (created) => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.message.success(doneText('created', 'Client', v['workspaceName'] as string));
        this.createdId = created.orgId;
        this.list.reload();
        this.secret.set({ title: 'Client created', password: created.temporaryPassword, facts: [{ label: 'Workspace code', value: created.signInKey }, { label: 'Administrator', value: created.adminEmail }] });
      },
      error: (e) => {
        this.saving.set(false);
        showDrawerSaveError(e, this.errors, this.message, 'Could not create the client');
      },
    });
  }

  /** Closing the "client created" dialog opens the new client. */
  afterCreated(): void {
    this.secret.set(null);
    if (this.createdId) void this.router.navigate(['/platform/clients', this.createdId]);
    this.createdId = null;
  }
}
