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
  styleUrl: './approvals.page.scss',
  templateUrl: './approvals.page.html',
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
