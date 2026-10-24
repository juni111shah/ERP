import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AdminApi, ApprovalDto } from '../../core/api/admin.api';
import { applyProblemDetailsToForm, asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { RevealDirective } from '../../shared/reveal.directive';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';

const COLORS: Record<string, string> = { pending: 'gold', approved: 'green', rejected: 'red', withdrawn: 'default' };

/** Approval inbox: requests waiting for me (approvers) and the ones I raised, with approve / reject / withdraw. */
@Component({
  selector: 'erp-approvals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSelectModule, TableToolsComponent, TranslatePipe, DecimalPipe, NzToolTipModule, RevealDirective, ReactiveFormsModule, DatePipe, HasPermissionDirective, NzTableModule, NzTabsModule, NzTagModule, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzInputNumberModule, NzIconModule],
  styles: [`.bar { display:flex; margin-bottom:12px } .spacer { flex:1 }`],
  template: `
    <h1 class="erp-page-title">Approvals</h1>
    <div class="bar"><span class="spacer"></span>
      <button nz-button nzType="primary" (click)="open()" data-testid="approval-new"><span nz-icon nzType="plus"></span> Request approval</button>
    </div>
    <nz-tabset (nzSelectedIndexChange)="tab.set($event); load()">
      <nz-tab nzTitle="Waiting for me"></nz-tab>
      <nz-tab nzTitle="My requests"></nz-tab>
    </nz-tabset>
    <erp-table-tools [(search)]="q" (refresh)="load()" [loading]="loading()" testId="approvals">
      <nz-select [ngModel]="status()" (ngModelChange)="status.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:170px" data-testid="approvals-status-filter"><nz-option nzValue="pending" [nzLabel]="'ui.status.pending' | translate: 'Pending'"></nz-option><nz-option nzValue="approved" [nzLabel]="'ui.status.approved' | translate: 'Approved'"></nz-option><nz-option nzValue="rejected" [nzLabel]="'ui.status.rejected' | translate: 'Rejected'"></nz-option><nz-option nzValue="withdrawn" [nzLabel]="'ui.status.withdrawn' | translate: 'Withdrawn'"></nz-option></nz-select>
    </erp-table-tools>
    <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04); margin-top:12px">
      <nz-table [nzData]="shown()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="approvals-table">
        <thead><tr><th>Subject</th><th>Type</th><th>Amount</th><th>Requested</th><th>Status</th><th>Actions</th></tr></thead>
        <tbody>
          @for (a of shown(); track a.id) {
            <tr>
              <td><span style="font-weight:600; color:#1e293b;">{{ a.subject }}</span> @if (a.justification) { <div style="color:var(--erp-color-text-secondary); font-size:12px; margin-top:2px;">{{ a.justification }}</div> }</td>
              <td><nz-tag>{{ a.documentType }}</nz-tag></td><td><strong>{{ a.amount != null ? (a.amount | number:'1.2-2') : '—' }}</strong></td><td>{{ a.requestedAt | date: 'yyyy-MM-dd HH:mm' }}</td>
              <td><nz-tag [nzColor]="color(a.status)">{{ a.status }}</nz-tag></td>
              <td>
                @if (a.status === 'pending') {
                  @if (tab() === 0) {
                    <span *erpHasPermission="'core.approvals.decide'"><a (click)="decide(a, true)" data-testid="approve" style="color:#10955d; font-weight:600;">Approve</a> · <a (click)="decide(a, false)" data-testid="reject" style="color:#ef4444; font-weight:600;">Reject</a></span>
                  } @else { <a (click)="withdraw(a)" data-testid="withdraw" style="color:#64748b;">Withdraw</a> }
                }
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>
    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="480" nzTitle="Request approval" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="approval-form">
          <nz-form-item><nz-form-label nzRequired>Subject</nz-form-label><nz-form-control [nzErrorTip]="err('subject')"><input nz-input formControlName="subject" data-testid="approval-subject" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Justification</nz-form-label><nz-form-control><textarea nz-input rows="3" formControlName="justification"></textarea></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Amount</nz-form-label><nz-form-control><nz-input-number formControlName="amount" [nzMin]="0" [nzPrecision]="2" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Cancel</button>
      <button nz-button nzType="primary" [nzLoading]="saving()" (click)="submit()" style="margin-inline-start:8px" data-testid="approval-save">Submit</button>
    </ng-template>`,
})
export class ApprovalsPage {
  private readonly api = inject(AdminApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  readonly tab = signal(0);
  readonly rows = signal<ApprovalDto[]>([]);
  readonly q = signal('');
  readonly status = signal<string | null>(null);
  /** Client-side filter: GET /admin/approvals only takes the tab scope. */
  readonly shown = computed(() => this.rows().filter((a) => matchText(this.q(), a.subject, a.justification, a.documentType) && (!this.status() || a.status === this.status())));
  readonly loading = signal(false);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly form = this.fb.group({ subject: ['', [Validators.required, Validators.maxLength(300)]], justification: [''], amount: [null as number | null] });

  constructor() { this.load(); }

  color(status: string): string { return COLORS[status] ?? 'default'; }
  err(name: string): string { return controlError(this.form.get(name)); }

  load(): void {
    this.loading.set(true);
    this.api.approvals(this.tab() === 1 ? 'mine' : undefined).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  open(): void { this.form.reset({ subject: '', justification: '', amount: null }); this.drawerOpen.set(true); }

  submit(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.submitApproval({ subject: v.subject, justification: v.justification || null, amount: v.amount, legalEntityId: null }).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Approval requested'); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not submit the request');
      },
    });
  }

  decide(a: ApprovalDto, approve: boolean): void {
    this.api.decide(a.id, approve).subscribe({ next: () => { this.message.success(approve ? 'Approved' : 'Rejected'); this.load(); }, error: (e) => this.fail(e) });
  }

  withdraw(a: ApprovalDto): void {
    this.api.withdraw(a.id).subscribe({ next: () => { this.message.success('Request withdrawn'); this.load(); }, error: (e) => this.fail(e) });
  }

  private fail(e: unknown): void { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); }
}
