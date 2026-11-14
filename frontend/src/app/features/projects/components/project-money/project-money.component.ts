import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { BudgetLineDto, EXPENSE_CATEGORIES, ProjectInvoice, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { ProjectDto } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { errorText, PRJ_STYLES, PRJ_SUBTABS } from '../../projects-ui';
import { budgetLineShare, capitalised } from '../../projects-view';
import { BillModalComponent } from '../bill-modal/bill-modal.component';

/** The money of a project: what can be billed, what was invoiced, and the budget by category against what was spent. */
@Component({
  selector: 'erp-project-money',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ErpDatePipe, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputNumberModule, NzSelectModule, HasPermissionDirective, BillModalComponent],
  templateUrl: './project-money.component.html',
  styles: [PRJ_STYLES + PRJ_SUBTABS + `
    .tiles { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; margin-bottom:12px; } .tile { display:flex; flex-direction:column; gap:4px; align-items:flex-start; } .tile b { font-size:18px; color:var(--erp-color-text-primary,#0f172a); } .small { font-size:11.5px; }
    .block { margin-bottom:12px; } .add { margin:10px 0 0; }
  `],
})
export class ProjectMoneyComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly bill = viewChild(BillModalComponent);
  readonly lines = signal<BudgetLineDto[]>([]);
  readonly invoices = signal<ProjectInvoice[]>([]);
  readonly categories = ['Labour', ...EXPENSE_CATEGORIES.map((c) => c.value)];
  readonly margin = computed(() => this.project().billedAmount + this.project().unbilledAmount - this.project().totalTimesheetCost);
  readonly plannedTotal = computed(() => this.lines().reduce((a, l) => a + l.amount, 0));
  newCategory: string | null = null; newAmount: number | null = null; newHours: number | null = null;

  constructor() { effect(() => { this.project().id; this.load(); }); }

  canBill(): boolean { return this.session.hasPermission('projects.billing.manage'); }
  canOpenInvoice(): boolean { return this.session.hasPermission('finance.invoices.read'); }

  load(): void {
    const id = this.project().id;
    loadOrFallback(this.lines, this.extra.budgetLines(id), []);
    if (this.session.hasPermission('projects.billing.read')) loadOrFallback(this.invoices, this.extra.projectInvoices(id), []);
  }

  label(status: string): string { return capitalised(status); }
  share(l: BudgetLineDto): number { return budgetLineShare(l); }
  min100(n: number): number { return Math.min(100, n); }

  /** Saves the typed budget line; a category that already has a line updates it. */
  saveLine(): void {
    const category = this.newCategory;
    if (!category) return;
    const existing = this.lines().find((l) => l.id && l.category.toLowerCase() === category.toLowerCase());
    this.extra.saveBudgetLine(existing?.id ?? null, { projectId: this.project().id, category, amount: this.newAmount ?? 0, hours: this.newHours ?? 0 }).subscribe({
      next: () => { this.newCategory = null; this.newAmount = null; this.newHours = null; this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not save the budget line.')),
    });
  }

  removeLine(l: BudgetLineDto): void {
    this.modal.confirm({
      nzTitle: `Delete the ${l.category} budget line?`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.extra.deleteBudgetLine(l.id!).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }),
    });
  }
}
