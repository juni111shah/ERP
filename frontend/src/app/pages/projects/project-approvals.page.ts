import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ProjectTimesheetsApi, TimesheetDto } from '../../core/api/projects.api';
import { ExpenseDto, ProjectExpensesApi, ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

/** Time and expenses waiting for a decision. Pick lines, then approve them or reject them with a reason. */
@Component({
  selector: 'erp-project-approvals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, FormsModule, NzButtonModule, NzCardModule, NzIconModule, NzInputModule, NzModalModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">Timesheet approvals</h1>
    <nz-card nzTitle="Time waiting for approval" class="card" data-testid="time-approvals">
      <nz-table #t [nzData]="time()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
        <thead><tr><th style="width:36px"><input type="checkbox" aria-label="Select all time" [checked]="allTime()" (change)="toggleAllTime($any($event.target).checked)" /></th><th>Date</th><th>Worker</th><th>Description</th><th style="text-align:right">Hours</th><th style="text-align:right">Amount</th></tr></thead>
        <tbody>
          @for (r of t.data; track r.id) {
            <tr><td><input type="checkbox" [attr.aria-label]="'Select ' + r.workerName + ' ' + r.date" [checked]="pickedTime().has(r.id)" (change)="pick(pickedTime, r.id, $any($event.target).checked)" /></td>
              <td>{{ r.date }}</td><td>{{ r.workerName }}</td><td>{{ r.description }}</td><td style="text-align:right">{{ r.hours | number: '1.2-2' }}</td><td style="text-align:right">{{ currency() }} {{ r.totalAmount | number: '1.2-2' }}</td></tr>
          } @empty { <tr><td colspan="6">Nothing is waiting.</td></tr> }
        </tbody>
      </nz-table>
      <div class="actions">
        <button nz-button nzType="primary" [disabled]="!pickedTime().size" (click)="decideTime(true)" data-testid="time-approve">Approve selected</button>
        <button nz-button nzDanger [disabled]="!pickedTime().size" (click)="askReason('time')" data-testid="time-reject">Reject selected</button>
      </div>
    </nz-card>

    <nz-card nzTitle="Expenses waiting for approval" class="card" data-testid="expense-approvals">
      <nz-table #e [nzData]="costs()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
        <thead><tr><th style="width:36px"><input type="checkbox" aria-label="Select all expenses" [checked]="allCosts()" (change)="toggleAllCosts($any($event.target).checked)" /></th><th>Date</th><th>Category</th><th>Description</th><th style="text-align:right">Amount</th></tr></thead>
        <tbody>
          @for (r of e.data; track r.id) {
            <tr><td><input type="checkbox" [attr.aria-label]="'Select ' + r.description" [checked]="pickedCosts().has(r.id)" (change)="pick(pickedCosts, r.id, $any($event.target).checked)" /></td>
              <td>{{ r.date }}</td><td>{{ r.category }}</td><td>{{ r.description }}</td><td style="text-align:right">{{ currency() }} {{ r.amount | number: '1.2-2' }}</td></tr>
          } @empty { <tr><td colspan="5">Nothing is waiting.</td></tr> }
        </tbody>
      </nz-table>
      <div class="actions">
        <button nz-button nzType="primary" [disabled]="!pickedCosts().size" (click)="decideCosts(true)" data-testid="expense-approve">Approve selected</button>
        <button nz-button nzDanger [disabled]="!pickedCosts().size" (click)="askReason('cost')" data-testid="expense-reject">Reject selected</button>
      </div>
    </nz-card>

    <nz-modal [nzVisible]="reasonFor() !== null" nzTitle="Why is it rejected?" nzOkText="Reject" (nzOnOk)="reject()" (nzOnCancel)="reasonFor.set(null)" [nzOkDanger]="true">
      <ng-container *nzModalContent>
        <textarea nz-input rows="3" [(ngModel)]="reason" maxlength="300" aria-label="Reason" data-testid="reject-reason"></textarea>
      </ng-container>
    </nz-modal>
  `,
  styles: ['.card { margin-bottom: 16px; border-radius: 12px; } .actions { display: flex; gap: 8px; margin-top: 12px; }'],
})
export class ProjectApprovalsPage {
  private readonly timeApi = inject(ProjectTimesheetsApi);
  private readonly costApi = inject(ProjectExpensesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly time = signal<TimesheetDto[]>([]);
  readonly costs = signal<ExpenseDto[]>([]);
  readonly pickedTime = signal(new Set<string>());
  readonly pickedCosts = signal(new Set<string>());
  readonly reasonFor = signal<'time' | 'cost' | null>(null);
  reason = '';

  constructor() { this.load(); }

  load(): void {
    const filters = [{ field: 'approvalStatus', op: 'eq' as const, value: 'Submitted' }];
    this.timeApi.list({ page: 1, pageSize: 200, filters }).subscribe((r) => { this.time.set(r.items); this.pickedTime.set(new Set()); });
    this.costApi.list({ page: 1, pageSize: 200, filters: [{ field: 'status', op: 'eq' as const, value: 'Submitted' }] }).subscribe((r) => { this.costs.set(r.items); this.pickedCosts.set(new Set()); });
  }

  allTime(): boolean { return this.time().length > 0 && this.pickedTime().size === this.time().length; }
  allCosts(): boolean { return this.costs().length > 0 && this.pickedCosts().size === this.costs().length; }
  toggleAllTime(on: boolean): void { this.pickedTime.set(on ? new Set(this.time().map((x) => x.id)) : new Set()); }
  toggleAllCosts(on: boolean): void { this.pickedCosts.set(on ? new Set(this.costs().map((x) => x.id)) : new Set()); }
  pick(target: ReturnType<typeof signal<Set<string>>>, id: string, on: boolean): void {
    const next = new Set(target()); if (on) next.add(id); else next.delete(id); target.set(next);
  }

  decideTime(approve: boolean, reason?: string): void {
    this.extra.decideTime([...this.pickedTime()], approve, reason).subscribe({ next: (r) => { this.msg.success(`${r.decided} time entr${r.decided === 1 ? 'y' : 'ies'} ${approve ? 'approved' : 'rejected'}`); this.load(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not decide.') });
  }
  decideCosts(approve: boolean, reason?: string): void {
    this.costApi.decide([...this.pickedCosts()], approve, reason).subscribe({ next: (r) => { this.msg.success(`${r.decided} expense(s) ${approve ? 'approved' : 'rejected'}`); this.load(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not decide.') });
  }
  askReason(kind: 'time' | 'cost'): void { this.reason = ''; this.reasonFor.set(kind); }
  reject(): void {
    if (!this.reason.trim()) { this.msg.warning('Say why it is rejected.'); return; }
    const kind = this.reasonFor(); this.reasonFor.set(null);
    if (kind === 'time') this.decideTime(false, this.reason.trim()); else this.decideCosts(false, this.reason.trim());
  }
}
