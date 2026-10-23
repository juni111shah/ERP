import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { MONTHS, PayrollApi, PayrollDto, PayrollLine } from './hr.api';

type Mode = 'pay' | 'adjust' | null;

/** One payroll run: totals, the employee lines (adjust a line for one-offs while draft), post, pay and the bank file. */
@Component({
  selector: 'erp-payroll-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzCardModule, NzModalModule, NzTableModule, NzTagModule],
  template: `
    @if (run(); as r) {
      <p><a routerLink="/hr/payroll">← Payroll</a></p>
      <h1 class="erp-page-title" data-testid="payroll-title">Payroll {{ period() }} <nz-tag [nzColor]="r.status === 'paid' ? 'green' : r.status === 'posted' ? 'blue' : 'default'">{{ r.status }}</nz-tag></h1>
      <p>{{ r.number }}</p>
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin:12px 0" data-testid="payroll-actions">
        @if (r.status === 'draft' || r.status === 'pendingApproval') {
          <button *erpHasPermission="'hr.payroll.manage'" nz-button [disabled]="busy()" (click)="recalculate()" data-testid="payroll-recalc">Recalculate</button>
          <button *erpHasPermission="'hr.payroll.post'" nz-button nzType="primary" [disabled]="busy()" (click)="post()" data-testid="payroll-post">{{ r.status === 'pendingApproval' ? 'Post (after approval)' : 'Submit and post' }}</button>
          <button *erpHasPermission="'hr.payroll.manage'" nz-button nzDanger [disabled]="busy()" (click)="remove()" data-testid="payroll-delete">Delete</button>
        }
        @if (r.status === 'posted') { <button *erpHasPermission="'hr.payroll.pay'" nz-button nzType="primary" (click)="mode.set('pay')" data-testid="payroll-pay">Pay salaries</button> }
        @if (r.status === 'posted' || r.status === 'paid') { <button *erpHasPermission="'hr.payroll.pay'" nz-button (click)="download()" data-testid="payroll-bankfile">Bank file (CSV)</button> <button *erpHasPermission="'hr.payroll.pay'" nz-button (click)="downloadWps()" data-testid="payroll-wps">WPS file (SIF)</button> }
      </div>
      <div style="display:flex;gap:16px;flex-wrap:wrap;margin-bottom:16px">
        <nz-card><small>Employees</small><div><b>{{ r.employees }}</b></div></nz-card>
        <nz-card><small>Gross earnings</small><div><b>{{ r.grossEarnings | number: '1.2-2' }}</b></div><small>after absence, incl. overtime</small></nz-card>
        <nz-card><small>Deductions</small><div><b>{{ r.deductions | number: '1.2-2' }}</b></div><small>advance / loan recoveries</small></nz-card>
        <nz-card><small>Net pay</small><div><b data-testid="payroll-net">{{ r.netPay | number: '1.2-2' }}</b></div>@if (r.paidOn) { <small>paid on {{ r.paidOn }}</small> }</nz-card>
      </div>
      <nz-table [nzData]="r.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="payroll-lines">
        <thead><tr><th>Employee</th><th>Absent</th><th>OT hrs</th><th>Basic</th><th>Allowances</th><th>Overtime</th><th>Absence</th><th>Adjustment</th><th>Gross earnings</th><th>Deductions</th><th>Net pay</th><th></th></tr></thead>
        <tbody>
          @for (l of r.lines; track l.id) {
            <tr>
              <td>{{ l.employeeName }}<div style="font-size:12px;color:var(--erp-color-text-secondary)">{{ l.employeeNumber }}</div></td>
              <td>{{ l.absentDays }}</td><td>{{ l.overtimeHours }}</td><td>{{ l.basic | number: '1.2-2' }}</td><td>{{ l.allowances | number: '1.2-2' }}</td><td>{{ l.overtime | number: '1.2-2' }}</td>
              <td>{{ l.absence | number: '1.2-2' }}</td><td>{{ l.adjustment | number: '1.2-2' }}</td><td>{{ l.grossEarnings | number: '1.2-2' }}</td><td>{{ l.deductions | number: '1.2-2' }}</td><td><b>{{ l.netPay | number: '1.2-2' }}</b></td>
              <td>@if (r.status === 'draft' || r.status === 'pendingApproval') { <a *erpHasPermission="'hr.payroll.manage'" (click)="startAdjust(l)" data-testid="payroll-adjust">Adjust</a> }</td>
            </tr>
          }
        </tbody>
      </nz-table>
      <erp-record-drawer [title]="mode() === 'pay' ? 'Pay salaries' : 'Adjust ' + (line()?.employeeName ?? '')" [open]="mode() !== null" [fields]="fields()" [saving]="busy()" [errors]="errors()" [width]="440"
                         testId="payroll-action-form" [submitLabel]="mode() === 'pay' ? 'Pay' : 'Save'" (submitted)="submit($event)" (closed)="mode.set(null)" />
    }`,
})
export class PayrollDetailPage {
  private readonly api = inject(PayrollApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly run = signal<PayrollDto | null>(null);
  readonly busy = signal(false);
  readonly mode = signal<Mode>(null);
  readonly line = signal<PayrollLine | null>(null);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly period = computed(() => { const r = this.run(); return r ? `${MONTHS[r.month - 1]} ${r.year}` : ''; });
  readonly fields = computed<FieldSpec[]>(() => this.mode() === 'pay'
    ? [
        { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
        { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
      ]
    : [
        { key: 'adjustment', label: 'One-off amount', type: 'number', required: true, default: this.line()?.adjustment ?? 0, hint: 'Positive adds to earnings, negative takes off.' },
        { key: 'note', label: 'Note', type: 'text', maxLength: 300, default: this.line()?.adjustmentNote ?? '' },
      ]);

  constructor() { this.load(); }

  startAdjust(l: PayrollLine): void { this.errors.set(null); this.line.set(l); this.mode.set('adjust'); }

  recalculate(): void { this.run$(this.api.recalculate(this.id), 'Recalculated'); }
  post(): void {
    this.modal.confirm({ nzTitle: 'Post this payroll?', nzContent: 'The accrual is posted to the ledger and the run can no longer be edited.', nzOkText: 'Post', nzOnOk: () => this.run$(this.api.post(this.id), 'Done') });
  }
  remove(): void {
    this.modal.confirm({ nzTitle: 'Delete this draft?', nzOkDanger: true, nzOkText: 'Delete', nzOnOk: () => new Promise<void>((resolve) => this.api.remove(this.id).subscribe({ next: () => { history.back(); resolve(); }, error: (e) => { this.fail(e); resolve(); } })) });
  }

  submit(v: Record<string, unknown>): void {
    if (this.mode() === 'pay') this.run$(this.api.pay(this.id, v['bankAccountId'] as string, v['date'] as string), 'Salaries paid');
    else this.run$(this.api.adjust(this.id, this.line()!.id, Number(v['adjustment'] ?? 0), (v['note'] as string) ?? null), 'Line adjusted');
  }

  download(): void {
    this.api.bankFile(this.id).subscribe((csv) => {
      const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
      const a = document.createElement('a'); a.href = url; a.download = `${this.run()?.number ?? 'payroll'}-bank-file.csv`; a.click(); URL.revokeObjectURL(url);
    });
  }

  downloadWps(): void {
    this.api.wpsFile(this.id).subscribe({
      next: (sif) => {
        const url = URL.createObjectURL(new Blob([sif], { type: 'text/csv;charset=utf-8' }));
        const a = document.createElement('a'); a.href = url; a.download = `${this.run()?.number ?? 'payroll'}.SIF`; a.click(); URL.revokeObjectURL(url);
      },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'The WPS file could not be prepared'),
    });
  }

  private run$(call: ReturnType<PayrollApi['post']>, success: string): Promise<void> {
    this.busy.set(true); this.errors.set(null);
    return new Promise((resolve) => call.subscribe({
      next: (r) => { this.busy.set(false); this.mode.set(null); this.run.set(r); this.message.success(r.status === 'pendingApproval' ? 'Sent for approval. Post it again once approved.' : success); resolve(); },
      error: (e) => { this.fail(e); resolve(); },
    }));
  }

  private fail(e: unknown): void { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); }
  private load(): void { this.api.detail(this.id).subscribe((r) => this.run.set(r)); }
}
