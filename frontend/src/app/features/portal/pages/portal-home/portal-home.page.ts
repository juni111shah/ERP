import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { OperatorFunction, catchError, of } from 'rxjs';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { PortalProjectsComponent } from '../../components/portal-projects/portal-projects.component';
import { portalMoney, PortalInvoice, PortalLease, PortalReceipt, PortalRequest, PortalService, PortalStatement } from '../../portal.service';

/** The tenant's own view: leases and rent schedule, invoices, receipts, statement and maintenance requests. */
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

@Component({
  selector: 'erp-portal-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, PortalProjectsComponent, TableToolsComponent, TranslatePipe, ReactiveFormsModule, NzSkeletonModule, NzTabsModule, NzTableModule, NzTagModule, NzButtonModule, NzFormModule, NzInputModule, NzSelectModule, PaginationBarComponent, StatCardComponent, StatePanelComponent, StatusTagComponent],
  styleUrl: './portal-home.page.scss',
  templateUrl: './portal-home.page.html',
})
export class PortalHomePage implements OnInit {
  readonly portal = inject(PortalService);
  readonly money = portalMoney;
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly leases = signal<PortalLease[]>([]);
  readonly invoices = signal<PortalInvoice[]>([]);
  readonly receipts = signal<PortalReceipt[]>([]);
  readonly requests = signal<PortalRequest[]>([]);
  readonly statement = signal<PortalStatement | null>(null);
  /** The tabs depend on whether this customer has a lease, so they wait for that answer. */
  readonly ready = signal(false);
  readonly failed = signal(false);
  readonly raising = signal(false);
  /** The statement has no currency of its own, so it takes the one on the customer's documents. */
  readonly code = computed(() => this.invoices()[0]?.currencyCode ?? this.leases()[0]?.currencyCode ?? this.receipts()[0]?.currencyCode ?? '');
  readonly openInvoices = computed(() => this.invoices().filter((i) => !i.isCreditNote && i.outstanding > 0).length);
  readonly openRequests = computed(() => this.requests().filter((r) => r.status !== 'completed' && r.status !== 'cancelled' && r.status !== 'closed').length);
  readonly invPg = new ClientPager(20);
  readonly recPg = new ClientPager(20);
  readonly reqPg = new ClientPager(20);
  /** Client-side search across the tabs' lists: the portal endpoints take no query parameters. */
  readonly q = signal('');
  readonly invoicesShown = computed(() => this.invoices().filter((r) => matchText(this.q(), r.number, r.status)));
  readonly receiptsShown = computed(() => this.receipts().filter((r) => matchText(this.q(), r.number, r.method, r.reference)));
  readonly requestsShown = computed(() => this.requests().filter((r) => matchText(this.q(), r.number, r.issue, r.status)));
  readonly form = inject(FormBuilder).nonNullable.group({ leaseId: ['', Validators.required], issue: ['', Validators.required], priority: ['normal'] });

  search(q: string): void { this.q.set(q); this.invPg.page.set(1); this.recPg.page.set(1); this.reqPg.page.set(1); }

  refresh(): void { this.ngOnInit(); }

  ngOnInit(): void {
    if (!this.portal.signedIn()) { void this.router.navigateByUrl('/portal/login'); return; }
    if (this.portal.kind() === 'vendor') { void this.router.navigateByUrl('/portal/vendor'); return; }
    this.failed.set(false);
    // A disabled module answers 403: that is not a failure, the customer simply has none of those records.
    const guard = <T>(fallback: T): OperatorFunction<T, T> => catchError((e: { status?: number }) => { if (e.status === 401) this.signOut(); else if (e.status !== 403 && e.status !== 404) this.failed.set(true); return of(fallback); });
    this.portal.leases().pipe(guard([] as PortalLease[])).subscribe((v) => { this.leases.set(v); this.ready.set(true); if (v.length === 1) this.form.patchValue({ leaseId: v[0].id }); });
    this.portal.invoices().pipe(guard([] as PortalInvoice[])).subscribe((v) => this.invoices.set(v));
    this.portal.receipts().pipe(guard([] as PortalReceipt[])).subscribe((v) => this.receipts.set(v));
    this.portal.statement().pipe(guard<PortalStatement | null>(null)).subscribe((v) => this.statement.set(v));
    this.loadRequests();
  }

  private loadRequests(): void { this.portal.requests().pipe(catchError(() => of([] as PortalRequest[]))).subscribe((v) => this.requests.set(v)); }

  raise(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.raising.set(true);
    this.portal.raise(this.form.getRawValue()).subscribe({
      next: () => { this.raising.set(false); this.message.success('Maintenance request sent'); this.form.patchValue({ issue: '' }); this.form.markAsPristine(); this.form.markAsUntouched(); this.loadRequests(); },
      error: () => { this.raising.set(false); this.message.error('Could not send the request'); },
    });
  }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }
}
