import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { BillableWork, BillInput, BillPreviewDto, ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { errorText, PRJ_STYLES } from './projects-ui';

/**
 * Raising an invoice for a project: pick the lines, choose how they are grouped, see exactly what the invoice will say, then create it.
 * Nothing is created before the person presses Create invoice. open(projectId, projectName); (billed) fires afterwards.
 */
@Component({
  selector: 'erp-bill-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzDatePickerModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule],
  template: `
    <nz-modal [nzVisible]="projectId() !== null" [nzTitle]="'Bill ' + projectName()" [nzWidth]="880" (nzOnCancel)="close()" [nzFooter]="footer">
      <ng-container *nzModalContent>
        @if (work(); as w) {
          <div class="lines" data-testid="bill-lines">
            @if (w.milestones.length) {
              <h4>Milestones ready to bill</h4>
              @for (m of w.milestones; track m.id) {
                <label class="line"><input type="checkbox" [checked]="pickedMilestones().has(m.id)" (change)="toggle(pickedMilestones, m.id, $any($event.target).checked)" [attr.aria-label]="'Bill ' + m.title" />
                  <span class="what">{{ m.title }}</span><span class="prj-muted">{{ m.achievedDate }}</span><span class="prj-num">{{ m.amount | number: '1.2-2' }}</span></label>
              }
            }
            @if (w.time.length) {
              <h4>Time <button nz-button nzType="link" nzSize="small" type="button" (click)="all(pickedTime, w.time, true)">All</button><button nz-button nzType="link" nzSize="small" type="button" (click)="all(pickedTime, w.time, false)">None</button></h4>
              @for (t of w.time; track t.id) {
                <label class="line"><input type="checkbox" [checked]="pickedTime().has(t.id)" (change)="toggle(pickedTime, t.id, $any($event.target).checked)" [attr.aria-label]="'Bill time of ' + t.worker + ' on ' + t.date" />
                  <span class="what">{{ t.worker }}{{ t.task ? ' · ' + t.task : '' }}{{ t.description ? ' · ' + t.description : '' }}</span><span class="prj-muted">{{ t.date }} · {{ t.hours | number: '1.0-2' }} h</span><span class="prj-num">{{ t.amount | number: '1.2-2' }}</span></label>
              }
            }
            @if (w.expenses.length) {
              <h4>Expenses</h4>
              @for (e of w.expenses; track e.id) {
                <label class="line"><input type="checkbox" [checked]="pickedExpenses().has(e.id)" (change)="toggle(pickedExpenses, e.id, $any($event.target).checked)" [attr.aria-label]="'Bill ' + e.description" />
                  <span class="what">{{ e.category }} · {{ e.description }}</span><span class="prj-muted">{{ e.date }}</span><span class="prj-num">{{ e.amount | number: '1.2-2' }}</span></label>
              }
            }
            @if (!w.time.length && !w.expenses.length && !w.milestones.length && w.billingType !== 'fixedFee') { <p class="prj-empty">Nothing is ready to bill on this project.</p> }
          </div>

          <div class="options" data-testid="bill-options">
            <label>Group the time by
              <nz-select [(ngModel)]="grouping" (ngModelChange)="preview()" aria-label="Group by">
                <nz-option nzValue="detailed" nzLabel="Each entry"></nz-option><nz-option nzValue="person" nzLabel="Person"></nz-option><nz-option nzValue="task" nzLabel="Task"></nz-option><nz-option nzValue="project" nzLabel="One line"></nz-option>
              </nz-select>
            </label>
            <label>Discount %<nz-input-number [(ngModel)]="discount" [nzMin]="0" [nzMax]="100" (ngModelChange)="preview()" aria-label="Discount percent"></nz-input-number></label>
            @if (w.billingType === 'fixedFee') {
              <label>Share of the fee %<nz-input-number [(ngModel)]="progress" [nzMin]="0" [nzMax]="100" (ngModelChange)="preview()" aria-label="Share of the fixed fee in percent" data-testid="bill-progress"></nz-input-number>
                <span class="prj-muted">{{ currency() }} {{ w.fixedFeeRemaining | number: '1.0-0' }} of {{ w.fee | number: '1.0-0' }} still to bill</span></label>
            }
            <label>Due date<nz-date-picker [(ngModel)]="dueDate" nzPlaceHolder="Customer's terms" aria-label="Invoice due date"></nz-date-picker></label>
            <label class="wide">Note on the invoice<input nz-input [(ngModel)]="notes" maxlength="500" aria-label="Invoice note" /></label>
          </div>
        }

        @if (previewError()) { <p class="prj-danger" role="alert">{{ previewError() }}</p> }
        @if (previewData(); as pv) {
          <h4>The invoice will say</h4>
          <div class="prj-table-wrap">
            <table class="pv" data-testid="bill-preview">
              <thead><tr><th>Line</th><th class="prj-num">Quantity</th><th class="prj-num">Price</th><th class="prj-num">Amount</th></tr></thead>
              <tbody>@for (l of pv.lines; track $index) { <tr><td>{{ l.description }}</td><td class="prj-num">{{ l.quantity | number: '1.0-2' }}</td><td class="prj-num">{{ l.price | number: '1.2-2' }}</td><td class="prj-num">{{ l.amount | number: '1.2-2' }}</td></tr> }</tbody>
            </table>
          </div>
          <p class="totals">Subtotal {{ currency() }} {{ pv.subtotal | number: '1.2-2' }}
            @if (pv.discount > 0) { · discount {{ pv.discount | number: '1.2-2' }} }
            · <b data-testid="bill-total">Total before tax {{ currency() }} {{ pv.total | number: '1.2-2' }}</b></p>
        }
      </ng-container>
    </nz-modal>
    <ng-template #footer>
      <button nz-button nzDanger type="button" (click)="writeOff()" [disabled]="!hasWork()" data-testid="bill-writeoff" title="Do not charge for the unbilled time and expenses of this project">Write off unbilled work</button>
      <button nz-button type="button" (click)="close()">Cancel</button>
      <button nz-button nzType="primary" type="button" (click)="generate()" [disabled]="!previewData()" [nzLoading]="busy()" data-testid="bill-generate">Create invoice</button>
    </ng-template>
  `,
  styles: [PRJ_STYLES + `
    h4 { font-size:13px; font-weight:600; margin:12px 0 6px; color:var(--erp-color-text-primary,#0f172a); display:flex; align-items:center; gap:4px; }
    .lines { max-height:240px; overflow:auto; border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:8px; padding:0 10px 8px; }
    .line { display:flex; gap:10px; align-items:center; padding:4px 0; cursor:pointer; font-size:13px; } .what { flex:1; min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .line .prj-num { width:90px; }
    .options { display:flex; gap:12px; flex-wrap:wrap; margin-top:12px; } .options label { display:flex; flex-direction:column; gap:4px; font-size:12.5px; min-width:150px; } .options nz-select, .options nz-input-number, .options nz-date-picker { width:100%; } .options .wide { flex:1; min-width:220px; }
    .pv { width:100%; border-collapse:collapse; font-size:13px; } .pv th { text-align:start; color:var(--erp-color-text-secondary,#64748b); font-weight:500; padding:4px 6px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); } .pv td { padding:4px 6px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); }
    .totals { text-align:end; margin:8px 0 0; }
  `],
})
export class BillModalComponent {
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  /** Fires after an invoice or a write-off. */
  readonly billed = output<void>();

  readonly projectId = signal<string | null>(null);
  readonly projectName = signal('');
  readonly work = signal<BillableWork | null>(null);
  readonly pickedTime = signal(new Set<string>());
  readonly pickedExpenses = signal(new Set<string>());
  readonly pickedMilestones = signal(new Set<string>());
  readonly previewData = signal<BillPreviewDto | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly busy = signal(false);
  readonly hasWork = computed(() => { const w = this.work(); return !!w && w.time.length + w.expenses.length > 0; });
  grouping = 'detailed'; discount = 0; progress: number | null = null; notes = ''; dueDate: Date | null = null;

  open(projectId: string, projectName: string): void {
    this.grouping = 'detailed'; this.discount = 0; this.progress = null; this.notes = ''; this.dueDate = null;
    this.previewData.set(null); this.previewError.set(null); this.work.set(null);
    this.projectName.set(projectName);
    this.projectId.set(projectId);
    this.api.billable(projectId).subscribe({
      next: (w) => {
        this.work.set(w);
        // ready-to-bill lines start ticked; people untick what should wait
        this.pickedTime.set(new Set(w.time.map((x) => x.id)));
        this.pickedExpenses.set(new Set(w.expenses.map((x) => x.id)));
        this.pickedMilestones.set(new Set(w.milestones.map((x) => x.id)));
        this.preview();
      },
      error: (e) => { this.previewError.set(errorText(e, 'Could not load the work to bill.')); },
    });
  }

  close(): void { this.projectId.set(null); }

  toggle(target: ReturnType<typeof signal<Set<string>>>, id: string, on: boolean): void {
    const next = new Set(target()); if (on) next.add(id); else next.delete(id); target.set(next);
    this.preview();
  }
  all(target: ReturnType<typeof signal<Set<string>>>, rows: { id: string }[], on: boolean): void {
    target.set(new Set(on ? rows.map((r) => r.id) : []));
    this.preview();
  }

  private input(): BillInput {
    return {
      projectId: this.projectId()!, timesheetIds: [...this.pickedTime()], expenseIds: [...this.pickedExpenses()], milestoneIds: [...this.pickedMilestones()],
      grouping: this.grouping, discountPercent: this.discount || 0, progressPercent: this.progress || null, notes: this.notes || null, dueDate: this.dueDate ? toIsoDate(this.dueDate) : null,
    };
  }

  preview(): void {
    if (!this.projectId() || !this.work()) return;
    this.previewError.set(null);
    this.api.previewBill(this.input()).subscribe({
      next: (r) => this.previewData.set(r),
      error: (e) => { this.previewData.set(null); this.previewError.set(errorText(e, 'Nothing to bill.')); },
    });
  }

  generate(): void {
    if (!this.projectId()) return;
    this.busy.set(true);
    this.api.bill(this.input()).subscribe({
      next: (res) => {
        this.busy.set(false); this.close(); this.billed.emit();
        const ref = this.msg.success(`Invoice ${res.invoiceNumber ?? ''} created as a draft (${this.currency()} ${res.totalAmountBilled.toFixed(2)})`);
        if (res.invoiceId) void this.router.navigate(['/finance/invoices', res.invoiceId]);
        return ref;
      },
      error: (e) => { this.busy.set(false); this.msg.error(errorText(e, 'Could not create the invoice.')); },
    });
  }

  writeOff(): void {
    const id = this.projectId(); if (!id) return;
    this.modal.confirm({
      nzTitle: `Write off the unbilled work of ${this.projectName()}?`,
      nzContent: 'All approved time and expenses that have not been billed will never be charged. Any cost already in work in progress moves to cost of sales. This cannot be undone.',
      nzOkText: 'Write off', nzOkDanger: true,
      nzOnOk: () => this.api.writeOffProject(id).subscribe({
        next: (r) => { this.msg.success(`${r.writtenOff} entries written off`); this.close(); this.billed.emit(); },
        error: (e) => this.msg.error(errorText(e, 'Could not write off.')),
      }),
    });
  }
}
