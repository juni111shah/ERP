import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, of } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { AdminApi, AuditApi, AuditEvent, TaxCodeDto, TaxCodesApi, UserSummary } from '../../core/api/admin.api';
import { asProblem } from '../../core/http/problem-details';
import { AttachmentsComponent } from '../../shared/attachments.component';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';
import { InvoiceExtras, InvoiceExtrasService, InvoiceSheetComponent, InvoiceSheetData, daysOverdue, formatMoney, humanizeSource } from './invoice-sheet.component';

interface Invoice extends InvoiceSheetData {
  legalEntityId: string;
  approvalRequestId?: string | null;
  journalEntryId: string | null;
  sourceId?: string | null;
}
interface Allocation { settlementId: string; number: string; date: string; method: string; reference?: string | null; currencyCode: string; amount: number; status: string; }
interface LeaseLink { invoiceId: string; leaseId: string; leaseNumber?: string | null; propertyId: string; propertyName?: string | null; unitNumber?: string | null; periodFrom: string; periodTo: string; }

@Component({
  selector: 'erp-invoice-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, NzCardModule, NzTableModule, NzTagModule, NzTimelineModule, NzModalModule, DetailPageComponent, InvoiceSheetComponent, AttachmentsComponent],
  styles: [`
    .section { margin-top: 16px; }
    .section ::ng-deep .ant-card-head-title { color:#b89b5e; text-transform:uppercase; letter-spacing:.08em; font-size:12px; font-weight:600; }
    .r { text-align:right; white-space:nowrap; }
    .muted { color:#8c8c8c; }
  `],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (invoice()) {
      @let inv = invoice()!;
      <erp-detail-page
        [backLink]="['/finance/invoices']"
        [backLabel]="'Back to Invoices'"
        [title]="(inv.isCreditNote ? 'Credit Note' : 'Invoice') + ' #' + inv.number"
        [status]="inv.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [hasDocumentsContent]="true"
        [hasHistoryContent]="history().length > 0"
        [createdAt]="inv.invoiceDate"
        [entityName]="inv.isCreditNote ? 'Credit Note' : 'Invoice'">

        <div content>
          <div class="section"><erp-invoice-sheet [invoice]="inv" [extras]="extras()" [taxRates]="taxRates()" /></div>

          <nz-card class="section" nzTitle="Payments" nzSize="small" data-testid="invoice-payments">
            @if (allocations().length) {
              <nz-table [nzData]="allocations()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
                <thead><tr><th>Receipt</th><th>Date</th><th>Method</th><th>Reference</th><th>Status</th><th class="r">Allocated</th></tr></thead>
                <tbody>
                  @for (a of allocations(); track a.settlementId) {
                    <tr>
                      <td><a [routerLink]="['/finance/receipts', a.settlementId]">{{ a.number }}</a></td>
                      <td>{{ a.date | date: 'dd MMM yyyy' }}</td><td>{{ humanize(a.method) }}</td><td>{{ a.reference || '—' }}</td>
                      <td><nz-tag>{{ humanize(a.status) }}</nz-tag></td><td class="r">{{ money(a.amount) }}</td>
                    </tr>
                  }
                </tbody>
              </nz-table>
            } @else { <span class="muted">No receipts allocated yet.</span> }
            @if (creditsApplied() > 0) {
              <div style="margin-top:8px" data-testid="invoice-credits"><b>Credits applied:</b> {{ money(creditsApplied()) }} <span class="muted">(settled by credit notes, not receipts)</span></div>
            }
          </nz-card>
        </div>

        <div documents><erp-attachments ownerType="invoice" [ownerId]="inv.id" /></div>

        <div history>
          <nz-timeline>
            @for (h of history(); track h.id) {
              <nz-timeline-item [nzColor]="h.action === 'Deleted' ? 'red' : h.action === 'Created' ? 'green' : 'blue'">
                <b>{{ h.action }}</b> · {{ h.occurredAt | date: 'dd MMM yyyy, HH:mm' }} · {{ userName(h.userId) }}
                @if (h.changedFields) { <div class="muted">{{ h.changedFields }}</div> }
              </nz-timeline-item>
            }
          </nz-timeline>
        </div>
      </erp-detail-page>
    }
  `
})
export class InvoiceDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly extrasApi = inject(InvoiceExtrasService);
  private readonly auditApi = inject(AuditApi);
  private readonly adminApi = inject(AdminApi);
  private readonly taxCodesApi = inject(TaxCodesApi);

  loading = signal(true);
  error = signal<string | null>(null);
  invoice = signal<Invoice | null>(null);
  extras = signal<InvoiceExtras | null>(null);
  allocations = signal<Allocation[]>([]);
  history = signal<AuditEvent[]>([]);
  users = signal<UserSummary[]>([]);
  leaseLink = signal<LeaseLink | null>(null);
  taxRates = signal<Record<string, number>>({});

  balance = computed(() => {
    const inv = this.invoice();
    return inv ? inv.total - inv.paid : 0;
  });

  daysOverdue = computed(() => {
    const inv = this.invoice();
    return inv ? daysOverdue(inv) : 0;
  });

  /** Part of "paid" not covered by receipts: applied credit notes leave no allocation row, so this is the difference. */
  creditsApplied = computed(() => {
    const inv = this.invoice();
    if (!inv || inv.isCreditNote) return 0;
    const received = this.allocations().reduce((s, a) => s + a.amount, 0);
    return Math.max(0, Math.round((inv.paid - received) * 100) / 100);
  });

  issuedBy = computed(() => {
    const created = this.history().find((h) => h.action === 'Created');
    return created?.userId ? this.userName(created.userId) : null;
  });

  actions = computed((): ActionButton[] => {
    const inv = this.invoice();
    if (!inv) return [];

    return [
      {
        label: 'Record Payment',
        icon: 'dollar',
        type: 'primary',
        permission: 'finance.receipts.write',
        visible: inv.status === 'issued' || inv.status === 'partlyPaid',
        handler: () => this.recordPayment()
      },
      {
        label: 'Credit Note',
        icon: 'file-add',
        permission: 'finance.invoices.write',
        visible: !inv.isCreditNote && (inv.status === 'issued' || inv.status === 'partlyPaid' || inv.status === 'paid'),
        handler: () => this.createCreditNote()
      },
      {
        // The API voids only an issued document with nothing paid or applied (409 otherwise).
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'finance.invoices.issue',
        visible: inv.status === 'issued' && inv.paid === 0,
        handler: () => this.voidInvoice()
      },
      {
        label: 'Duplicate',
        icon: 'copy',
        permission: 'finance.invoices.write',
        handler: () => this.duplicate()
      },
      {
        label: 'View Journal',
        icon: 'book',
        visible: !!inv.journalEntryId,
        handler: () => this.viewJournal(inv.journalEntryId!)
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => this.print(inv.id)
      },
      {
        label: 'Email',
        icon: 'mail',
        handler: () => this.email()
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const inv = this.invoice();
    if (!inv) return [];

    return [
      { title: 'Total Amount', value: formatMoney(inv.total), suffix: inv.currencyCode },
      { title: 'Amount Paid', value: formatMoney(inv.paid), suffix: inv.currencyCode },
      { title: 'Balance Due', value: formatMoney(this.balance()), suffix: inv.currencyCode, valueStyle: { color: this.balance() > 0 ? '#ff4d4f' : '#52c41a' } },
      { title: 'Days Overdue', value: this.daysOverdue() || 0, valueStyle: { color: this.daysOverdue() > 0 ? '#ff4d4f' : '#52c41a' } }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const inv = this.invoice();
    if (!inv) return [];
    const fmt = (d: string) => new Date(d + 'T00:00:00').toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' });
    const h = this.extras()?.header;
    const items: DescriptionItem[] = [];
    if (h) items.push({ label: 'Company', value: h.legalEntityName || h.organizationName });
    items.push({ label: 'Customer', value: inv.partyName || 'Customer', routerLink: ['/parties', inv.partyId] });
    if (this.issuedBy()) items.push({ label: 'Issued by', value: this.issuedBy() });
    const link = this.leaseLink();
    if (link) {
      items.push({ label: 'Service period', value: `${fmt(link.periodFrom)} – ${fmt(link.periodTo)}` });
      items.push({ label: 'Lease', value: link.leaseNumber ?? '—' });
      items.push({ label: 'Property', value: [link.propertyName, link.unitNumber ? `unit ${link.unitNumber}` : ''].filter((x) => !!x).join(', ') });
    }
    const src = humanizeSource(inv.sourceType);
    if (src) items.push({ label: 'Source', value: src });
    if (inv.creditNoteForId) items.push({ label: 'Credit Note For', value: 'View original invoice', routerLink: ['/finance/invoices', inv.creditNoteForId] });
    return items;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid invoice ID');
      this.loading.set(false);
      return;
    }

    this.http.get<Invoice>(`${this.apiBase}/finance/invoices/${id}`).subscribe({
      next: (response) => {
        this.invoice.set(response);
        this.loading.set(false);
        this.loadSections(response);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load invoice');
        this.loading.set(false);
      }
    });
  }

  /** Each optional section loads on its own and simply stays hidden when the user lacks the permission or there is no data. */
  private loadSections(inv: Invoice): void {
    this.extrasApi.load(inv.id).subscribe((x) => this.extras.set(x));
    this.taxCodesApi.list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] })))
      .subscribe((r) => this.taxRates.set(Object.fromEntries(r.items.map((t) => [t.id, t.rate]))));
    this.http.get<Allocation[]>(`${this.apiBase}/finance/invoices/${inv.id}/allocations`).pipe(catchError(() => of([] as Allocation[])))
      .subscribe((r) => this.allocations.set(r ?? []));
    this.auditApi.list({ page: 1, pageSize: 50, sort: 'occurredAt', filters: [{ field: 'recordId', op: 'eq', value: inv.id }] })
      .pipe(catchError(() => of({ items: [] as AuditEvent[] })))
      .subscribe((r) => {
        this.history.set(r.items ?? []);
        if (r.items?.some((h) => h.userId)) this.adminApi.users().pipe(catchError(() => of([] as UserSummary[]))).subscribe((u) => this.users.set(u ?? []));
      });
    if ((inv.sourceType ?? '').toLowerCase().startsWith('realestate.')) {
      this.http.get<LeaseLink[]>(`${this.apiBase}/m/realestate/billing/invoice-links`, { params: { ids: [inv.id] } }).pipe(catchError(() => of([] as LeaseLink[])))
        .subscribe((r) => this.leaseLink.set(r?.[0] ?? null));
    }
  }

  money(v: number): string { return formatMoney(v, this.invoice()?.currencyCode); }
  humanize(v: string | null | undefined): string {
    if (!v) return '';
    const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2');
    return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
  }
  userName(id?: string | null): string {
    if (!id) return 'System';
    const u = this.users().find((x) => x.id === id);
    return u ? u.fullName || u.email : 'User';
  }

  recordPayment(): void {
    this.message.info('Record payment functionality will be implemented');
  }

  createCreditNote(): void {
    this.message.info('Create credit note functionality will be implemented');
  }

  voidInvoice(): void {
    const inv = this.invoice();
    if (!inv) return;
    this.modal.confirm({
      nzTitle: 'Void this invoice?',
      nzContent: 'This reverses its journal and cannot be undone. The invoice will be marked as voided.',
      nzOkText: 'Void Invoice',
      nzOkDanger: true,
      nzOnOk: () => this.http.post<Invoice>(`${this.apiBase}/finance/invoices/${inv.id}/void`, {}).subscribe({
        next: (r) => { this.invoice.set(r); this.message.success('Invoice voided'); },
        error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not void the invoice'); },
      })
    });
  }

  duplicate(): void {
    this.message.info('Duplicate functionality will be implemented');
  }

  viewJournal(journalId: string): void {
    this.router.navigate(['/finance/journals', journalId]);
  }

  print(id: string): void {
    window.open(`/print/invoice/${id}`, '_blank');
  }

  email(): void {
    this.message.info('Email functionality will be implemented');
  }
}
