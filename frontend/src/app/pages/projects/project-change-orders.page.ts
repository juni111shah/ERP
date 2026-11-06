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
import { ProjectsApi } from '../../core/api/projects.api';
import { ChangeOrderDto, ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { errorText, PRJ_STYLES, ProjectTagComponent } from './projects-ui';

/** Scope changes agreed with the customer: an approved change order raises (or lowers) the project budget and fixed fee. */
@Component({
  selector: 'erp-project-change-orders',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  template: `
    <div class="prj-toolbar">
      <span class="prj-muted">{{ summary() }}</span>
      <span class="prj-spacer"></span>
      <button *erpHasPermission="'projects.changeorders.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="co-new"><span nz-icon nzType="plus"></span> New change order</button>
    </div>
    <div class="prj-table-wrap">
      <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzNoResult]="' '" data-testid="co-table">
        <thead><tr><th>Number</th>@if (!projectId()) { <th>Project</th> }<th>Title</th><th class="prj-num">Change to budget</th><th class="prj-num">Change to hours</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (c of rows(); track c.id) {
            <tr>
              <td>{{ c.number }}</td>
              @if (!projectId()) { <td><a [routerLink]="['/projects', c.projectId]" [queryParams]="{ tab: 'risks' }" class="prj-link">{{ projectName(c.projectId) }}</a></td> }
              <td><span class="prj-strong">{{ c.title }}</span>@if (c.description) { <div class="prj-muted">{{ c.description }}</div> }@if (c.decisionNote) { <div class="prj-muted">Decision: {{ c.decisionNote }}</div> }</td>
              <td class="prj-num">{{ currency() }} {{ c.amountChange | number: '1.2-2' }}</td><td class="prj-num">{{ c.hoursChange | number: '1.0-1' }}</td>
              <td><erp-prj-tag kind="change" [value]="c.status" /></td>
              <td class="prj-num">
                <ng-container *erpHasPermission="'projects.changeorders.manage'">
                  @if (c.status === 'draft') {
                    <button nz-button nzSize="small" (click)="state.edit(c)">Edit</button>
                    <button nz-button nzSize="small" nzType="primary" (click)="decide(c, true)" [attr.data-testid]="'co-approve-' + c.number">Approve</button>
                    <button nz-button nzSize="small" nzDanger (click)="decide(c, false)">Reject</button>
                    <button nz-button nzSize="small" nzType="text" nzDanger (click)="remove(c)" aria-label="Delete change order"><span nz-icon nzType="delete"></span></button>
                  }
                </ng-container>
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
      @if (!rows().length) { <div class="prj-empty">No change orders yet.</div> }
    </div>
    <erp-record-drawer [title]="state.editing() ? 'Edit change order' : 'New change order'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="co-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    <nz-modal [nzVisible]="deciding() !== null" [nzTitle]="deciding()?.approve ? 'Approve change order' : 'Reject change order'" nzOkText="Confirm" [nzOkDanger]="!deciding()?.approve" (nzOnOk)="confirmDecision()" (nzOnCancel)="deciding.set(null)">
      <ng-container *nzModalContent>
        <p>{{ deciding()?.order?.number }} · {{ deciding()?.order?.title }}. @if (deciding()?.approve) { The project budget will change by {{ currency() }} {{ deciding()?.order?.amountChange | number: '1.2-2' }}. }</p>
        <label class="erp-inline-field" style="display:flex">Note (for example who agreed)<textarea nz-input rows="3" [(ngModel)]="note" maxlength="500" data-testid="co-note"></textarea></label>
      </ng-container>
    </nz-modal>
  `,
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
    this.api.decideChangeOrder(d.order.id, d.approve, this.note || undefined).subscribe({ next: () => { this.msg.success(d.approve ? 'Approved. The project budget now includes it.' : 'Rejected'); this.load(); }, error: (e) => this.msg.error(errorText(e, 'Could not save the decision.')) });
  }
  remove(c: ChangeOrderDto): void {
    this.modal.confirm({ nzTitle: 'Delete this change order?', nzContent: c.title, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteChangeOrder(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
