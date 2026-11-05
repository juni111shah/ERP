import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { ChangeOrderDto, ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

const COLORS: Record<string, string> = { Draft: 'default', Approved: 'green', Rejected: 'red' };

/** Scope changes agreed with the customer: an approved change order raises (or lowers) the project budget and fixed fee. */
@Component({
  selector: 'erp-project-change-orders',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzTableModule, NzTagModule, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <h1 class="erp-page-title">Change orders</h1>
    <div style="margin-bottom:12px">
      <button *erpHasPermission="'projects.changeorders.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="co-new"><span nz-icon nzType="plus"></span> New change order</button>
    </div>
    <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="co-table">
      <thead><tr><th>Number</th><th>Project</th><th>Title</th><th style="text-align:right">Amount change</th><th style="text-align:right">Hours change</th><th>Status</th><th></th></tr></thead>
      <tbody>
        @for (c of rows(); track c.id) {
          <tr>
            <td>{{ c.number }}</td><td><a [routerLink]="['/projects', c.projectId]">{{ projectName(c.projectId) }}</a></td><td>{{ c.title }}</td>
            <td style="text-align:right">{{ currency() }} {{ c.amountChange | number: '1.2-2' }}</td><td style="text-align:right">{{ c.hoursChange | number: '1.0-1' }}</td>
            <td><nz-tag [nzColor]="color(c.status)" [title]="c.decisionNote || ''">{{ c.status }}</nz-tag></td>
            <td *erpHasPermission="'projects.changeorders.manage'">
              @if (c.status === 'Draft') {
                <button nz-button nzSize="small" (click)="state.edit(c)">Edit</button>
                <button nz-button nzSize="small" nzType="primary" (click)="decide(c, true)" [attr.data-testid]="'co-approve-' + c.number">Approve</button>
                <button nz-button nzSize="small" nzDanger (click)="decide(c, false)">Reject</button>
                <button nz-button nzSize="small" nzDanger (click)="remove(c)">Delete</button>
              }
            </td>
          </tr>
        } @empty { <tr><td colspan="7">No change orders yet.</td></tr> }
      </tbody>
    </nz-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit change order' : 'New change order'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="co-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    <nz-modal [nzVisible]="deciding() !== null" [nzTitle]="deciding()?.approve ? 'Approve change order' : 'Reject change order'" nzOkText="Confirm" (nzOnOk)="confirmDecision()" (nzOnCancel)="deciding.set(null)">
      <ng-container *nzModalContent><textarea nz-input rows="3" [(ngModel)]="note" maxlength="500" placeholder="Note (for example who agreed)" aria-label="Decision note"></textarea></ng-container>
    </nz-modal>
  `,
})
export class ProjectChangeOrdersPage {
  private readonly api = inject(ProjectsExtraApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly rows = signal<ChangeOrderDto[]>([]);
  readonly deciding = signal<{ order: ChangeOrderDto; approve: boolean } | null>(null);
  note = '';
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });

  readonly state = new MasterDataState<ChangeOrderDto>({
    save: (id, body) => this.api.saveChangeOrder(id, body as never), reload: () => this.load(), message: this.msg, entityName: 'Change order',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'description', label: 'What changes', type: 'textarea', maxLength: 2000 },
    { key: 'amountChange', label: 'Change to the budget / fee', type: 'number', required: true, hint: 'Negative to reduce it.' },
    { key: 'hoursChange', label: 'Change to the budgeted hours', type: 'number', default: 0 },
  ]);

  constructor() { this.load(); }

  load(): void { this.api.changeOrders().subscribe((r) => this.rows.set(r)); }
  color(s: string): string { return COLORS[s] ?? 'default'; }
  projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  decide(order: ChangeOrderDto, approve: boolean): void { this.note = ''; this.deciding.set({ order, approve }); }
  confirmDecision(): void {
    const d = this.deciding(); if (!d) return;
    this.deciding.set(null);
    this.api.decideChangeOrder(d.order.id, d.approve, this.note || undefined).subscribe({ next: () => { this.msg.success(d.approve ? 'Approved. The project budget now includes it.' : 'Rejected'); this.load(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not save the decision.') });
  }
  remove(c: ChangeOrderDto): void {
    this.modal.confirm({ nzTitle: 'Delete this change order?', nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteChangeOrder(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete.') }) });
  }
}
