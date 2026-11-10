import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AdminApi, ApprovalDto } from '@core/api/admin.api';
import { applyProblemDetailsToForm, asProblem, problemMessage, controlError } from '@core/http/problem-details';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** Approval inbox: requests waiting for me (approvers) and the ones I raised, with approve / reject / withdraw. */
@Component({
  selector: 'erp-approvals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, ErpLabelPipe, ErpDatePipe, StatusTagComponent, StatePanelComponent, FormsModule, NzSelectModule, TableToolsComponent, TranslatePipe, DecimalPipe, NzToolTipModule, RevealDirective, ReactiveFormsModule, NzTableModule, NzTabsModule, NzTagModule, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzInputNumberModule, NzIconModule],
  styles: [`.bar { display:flex; margin-bottom:12px } .spacer { flex:1 } .num { text-align:end; font-variant-numeric:tabular-nums; white-space:nowrap } .nowrap { white-space:nowrap }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius:12px; overflow:hidden; margin-top:12px }
    .subject { font-weight:600; color: var(--erp-color-text-primary) } .level { font-size:12px; color: var(--erp-color-text-muted); margin-top:2px }`],
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
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the approvals" hint="The server did not return the list." actionLabel="Try again" (action)="load()" style="margin-top:12px" />
    } @else {
    <div class="card">
      <nz-table [nzData]="shown()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" [nzNoResult]="tab() === 0 ? 'Nothing is waiting for you.' : 'You have not requested any approvals.'" data-testid="approvals-table">
        <thead><tr><th>Subject</th><th>Type</th><th class="num">Amount</th><th>Requested</th><th>Status</th><th>Actions</th></tr></thead>
        <tbody>
          @for (a of shown(); track a.id) {
            <tr>
              <td><span class="subject">{{ a.subject }}</span> @if (a.justification) { <div style="color:var(--erp-color-text-secondary); font-size:12px; margin-top:2px;">{{ a.justification }}</div> }</td>
              <td><nz-tag>{{ a.documentType | erpLabel }}</nz-tag></td><td class="num"><strong>{{ a.amount != null ? (a.amount | number:'1.2-2') : '—' }}</strong></td><td class="nowrap">{{ a.requestedAt | erpDate }}</td>
              <td>
                <erp-status [value]="a.status" />
                @if (a.status === 'pending' && a.currentLevel) {
                  <div class="level" data-testid="approval-level">Level {{ a.currentLevel }}@if (levels(a) > 1) { of {{ levels(a) }} }</div>
                }
              </td>
              <td class="nowrap">
                @if (a.status === 'pending') {
                  @if (tab() === 0) {
                    <!-- The list holds only what this person can decide now; the server checks the permission of the level. -->
                    <button nz-button nzType="primary" nzSize="small" type="button" (click)="decide(a, true)" data-testid="approve">Approve</button> <button nz-button nzDanger nzSize="small" type="button" (click)="decide(a, false)" data-testid="reject">Reject</button>
                  } @else { <button nz-button nzSize="small" type="button" (click)="withdraw(a)" data-testid="withdraw">Withdraw</button> }
                }
                <button nz-button nzType="link" nzSize="small" type="button" (click)="showSteps(a)" data-testid="approval-steps">Steps</button>
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>
    }
    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="480" nzTitle="Request approval" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="approval-form">
          <nz-form-item><nz-form-label nzRequired>Subject</nz-form-label><nz-form-control [nzErrorTip]="err('subject')"><input nz-input formControlName="subject" data-testid="approval-subject" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Justification</nz-form-label><nz-form-control><textarea nz-input rows="3" formControlName="justification"></textarea></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Amount</nz-form-label><nz-form-control><nz-input-number formControlName="amount" [nzMin]="0" [nzPrecision]="2" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
        </form>
      </ng-container>
    </nz-drawer>
    <nz-modal [nzVisible]="!!detail()" nzTitle="Approval steps" [nzFooter]="null" [nzWidth]="640" (nzOnCancel)="detail.set(null)">
      <ng-container *nzModalContent>
        @if (detail(); as d) {
          <p><b>{{ d.subject }}</b></p>
          @if (d.steps?.length) {
            <nz-table [nzData]="d.steps!" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="approval-steps-table">
              <thead><tr><th>Level</th><th>Needs permission</th><th>Status</th><th>Decided</th><th>Comment</th></tr></thead>
              <tbody>
                @for (s of d.steps!; track s.id) {
                  <tr><td>{{ s.level }}</td><td><code>{{ s.approverPermission }}</code></td><td><erp-status [value]="s.status" /></td><td class="nowrap">{{ s.decidedAt ? (s.decidedAt | erpDate) : '—' }}</td><td>{{ s.comment || '—' }}</td></tr>
                }
              </tbody>
            </nz-table>
          } @else { <p>This request has one decision and no steps were recorded.</p> }
        }
      </ng-container>
    </nz-modal>
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
  readonly failed = signal(false);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly form = this.fb.group({ subject: ['', [Validators.required, Validators.maxLength(300)]], justification: [''], amount: [null as number | null] });

  readonly detail = signal<ApprovalDto | null>(null);

  constructor() { this.load(); }

  /** How many levels the request has (steps that were cancelled or never reached still count). */
  levels(a: ApprovalDto): number { return a.steps?.length ?? 0; }

  showSteps(a: ApprovalDto): void {
    this.detail.set(a);
    this.api.approval(a.id).subscribe({ next: (full) => this.detail.set(full), error: () => undefined });
  }

  err(name: string): string { return controlError(this.form.get(name)); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.approvals(this.tab() === 1 ? 'mine' : undefined).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => { this.failed.set(true); this.loading.set(false); } });
  }

  open(): void { this.form.reset({ subject: '', justification: '', amount: null }); this.drawerOpen.set(true); }

  submit(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.submitApproval({ subject: v.subject, justification: v.justification || null, amount: v.amount, legalEntityId: null }).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(doneText('submitted', 'Approval request', v.subject)); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not submit the request');
      },
    });
  }

  decide(a: ApprovalDto, approve: boolean): void {
    this.api.decide(a.id, approve).subscribe({ next: () => { this.message.success(doneText(approve ? 'approved' : 'rejected', 'Approval request', a.subject)); this.load(); }, error: (e) => this.fail(e) });
  }

  withdraw(a: ApprovalDto): void {
    this.api.withdraw(a.id).subscribe({ next: () => { this.message.success(`Approval request ${a.subject} withdrawn`); this.load(); }, error: (e) => this.fail(e) });
  }

  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Action failed')); }
}
