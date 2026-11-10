import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '@core/api/projects.api';
import { ChangeOrderDto, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { errorText, PRJ_STYLES, ProjectTagComponent } from '../../projects-ui';

/** Scope changes agreed with the customer: an approved change order raises (or lowers) the project budget and fixed fee. */
@Component({
  selector: 'erp-project-change-orders',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  templateUrl: './project-change-orders.page.html',
  styles: [PRJ_STYLES],
})
export class ProjectChangeOrdersPage {
  /** Set by the project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly api = inject(ProjectsExtraApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly rows = signal<ChangeOrderDto[]>([]);
  readonly deciding = signal<{ order: ChangeOrderDto; approve: boolean } | null>(null);
  note = '';
  private readonly projects = toSignal(this.projectsApi.open().pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });

  readonly summary = computed(() => {
    const approved = this.rows().filter((c) => c.status === 'approved').reduce((a, c) => a + c.amountChange, 0);
    const waiting = this.rows().filter((c) => c.status === 'draft').length;
    return `${waiting} waiting for a decision · ${this.currency()} ${approved.toLocaleString('en-US')} approved`;
  });

  readonly state: MasterDataState<ChangeOrderDto> = new MasterDataState<ChangeOrderDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) };
      b['projectId'] = this.projectId() ?? b['projectId'] ?? this.state.editing()?.projectId;
      return this.api.saveChangeOrder(id, b as never);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Change order',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    ...(this.projectId() ? [] : [{ key: 'projectId', label: 'Project', type: 'select' as const, required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) }]),
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'description', label: 'What changes', type: 'textarea', maxLength: 2000 },
    { key: 'amountChange', label: `Change to the budget or fee (${this.currency()})`, type: 'number', required: true, hint: 'Negative to reduce it.' },
    { key: 'hoursChange', label: 'Change to the budgeted hours', type: 'number', default: 0 },
  ]);

  constructor() { effect(() => { this.projectId(); this.load(); }); }

  load(): void { this.api.changeOrders(this.projectId()).subscribe({ next: (r) => this.rows.set(r), error: (e) => this.msg.error(errorText(e, 'Could not load the change orders.')) }); }
  projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Open project'; }
  decide(order: ChangeOrderDto, approve: boolean): void { this.note = ''; this.deciding.set({ order, approve }); }
  confirmDecision(): void {
    const d = this.deciding(); if (!d) return;
    this.deciding.set(null);
    this.api.decideChangeOrder(d.order.id, d.approve, this.note || undefined).subscribe({ next: () => { this.msg.success(d.approve ? `${d.order.number} approved. The project budget now includes it.` : `${d.order.number} rejected`); this.load(); }, error: (e) => this.msg.error(errorText(e, 'Could not save the decision.')) });
  }
  remove(c: ChangeOrderDto): void {
    this.modal.confirm({ nzTitle: 'Delete this change order?', nzContent: c.title, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteChangeOrder(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
