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
import { catchError, of } from 'rxjs';
import { PortalInvoice, PortalLease, PortalReceipt, PortalRequest, PortalService, PortalStatement } from '@core/api/portal.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { PortalProjectsComponent } from '../../components/portal-projects/portal-projects.component';
import { PortalLoadHandlers, portalFallback, portalMoney } from '../../portal-view';

/** The tenant's own view: leases and rent schedule, invoices, receipts, statement and maintenance requests. */
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
  private readonly onLoadError: PortalLoadHandlers = { signOut: () => this.signOut(), failed: () => this.failed.set(true) };

  search(q: string): void { this.q.set(q); this.invPg.page.set(1); this.recPg.page.set(1); this.reqPg.page.set(1); }

  refresh(): void { this.ngOnInit(); }

  ngOnInit(): void {
    if (!this.portal.signedIn()) { void this.router.navigateByUrl('/portal/login'); return; }
    if (this.portal.kind() === 'vendor') { void this.router.navigateByUrl('/portal/vendor'); return; }
    this.failed.set(false);
    this.portal.leases().pipe(portalFallback([] as PortalLease[], this.onLoadError)).subscribe((v) => { this.leases.set(v); this.ready.set(true); if (v.length === 1) this.form.patchValue({ leaseId: v[0].id }); });
    this.portal.invoices().pipe(portalFallback([] as PortalInvoice[], this.onLoadError)).subscribe((v) => this.invoices.set(v));
    this.portal.receipts().pipe(portalFallback([] as PortalReceipt[], this.onLoadError)).subscribe((v) => this.receipts.set(v));
    this.portal.statement().pipe(portalFallback<PortalStatement | null>(null, this.onLoadError)).subscribe((v) => this.statement.set(v));
    this.loadRequests();
  }

  raise(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.raising.set(true);
    this.portal.raise(this.form.getRawValue()).subscribe({
      next: () => { this.raising.set(false); this.message.success('Maintenance request sent'); this.form.patchValue({ issue: '' }); this.form.markAsPristine(); this.form.markAsUntouched(); this.loadRequests(); },
      error: () => { this.raising.set(false); this.message.error('Could not send the request'); },
    });
  }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }

  private loadRequests(): void { this.portal.requests().pipe(catchError(() => of([] as PortalRequest[]))).subscribe((v) => this.requests.set(v)); }
}
