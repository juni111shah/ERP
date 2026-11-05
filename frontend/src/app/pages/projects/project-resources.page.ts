import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { AllocationDto, ProjectsExtraApi, RateDto, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

/** Who works on what and for how much: rate cards (bill and cost per hour) and planned allocations. */
@Component({
  selector: 'erp-project-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, NzButtonModule, NzIconModule, NzTableModule, NzTabsModule, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <h1 class="erp-page-title">Resources</h1>
    <nz-tabs>
      <nz-tab nzTitle="Rate cards">
        <p class="hint">The most specific rate wins: a person on a project, then the person, then a role on the project, then the project, then everyone. Time logged without a rate uses this.</p>
        <div style="margin-bottom:12px"><button *erpHasPermission="'projects.rates.manage'" nz-button nzType="primary" (click)="rateState.create()" data-testid="rate-new"><span nz-icon nzType="plus"></span> New rate</button></div>
        <nz-table [nzData]="rates()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rate-table">
          <thead><tr><th>Applies to</th><th>Project</th><th style="text-align:right">Bill rate / hour</th><th style="text-align:right">Cost rate / hour</th><th style="text-align:right">Margin</th><th></th></tr></thead>
          <tbody>
            @for (r of rates(); track r.id) {
              <tr><td>{{ r.userName || r.role || 'Everyone' }}</td><td>{{ projectName(r.projectId) }}</td><td style="text-align:right">{{ currency() }} {{ r.billRate | number: '1.2-2' }}</td><td style="text-align:right">{{ currency() }} {{ r.costRate | number: '1.2-2' }}</td>
                <td style="text-align:right">{{ r.billRate > 0 ? ((r.billRate - r.costRate) / r.billRate * 100 | number: '1.0-0') + '%' : '' }}</td>
                <td *erpHasPermission="'projects.rates.manage'"><button nz-button nzSize="small" (click)="rateState.edit(r)">Edit</button> <button nz-button nzSize="small" nzDanger (click)="removeRate(r)">Delete</button></td></tr>
            } @empty { <tr><td colspan="6">No rate cards. Time uses the workspace default rate.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab nzTitle="Allocations">
        <p class="hint">Hours a week a person is planned to give a project. The utilisation report compares this with capacity and with the time actually logged.</p>
        <div style="margin-bottom:12px"><button *erpHasPermission="'projects.team.manage'" nz-button nzType="primary" (click)="allocState.create()" data-testid="alloc-new"><span nz-icon nzType="plus"></span> New allocation</button></div>
        <nz-table [nzData]="allocations()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="alloc-table">
          <thead><tr><th>Person</th><th>Project</th><th>From</th><th>To</th><th style="text-align:right">Hours / week</th><th></th></tr></thead>
          <tbody>
            @for (a of allocations(); track a.id) {
              <tr><td>{{ a.userName }}</td><td>{{ projectName(a.projectId) }}</td><td>{{ a.fromDate }}</td><td>{{ a.toDate }}</td><td style="text-align:right">{{ a.hoursPerWeek | number: '1.0-1' }}</td>
                <td *erpHasPermission="'projects.team.manage'"><button nz-button nzSize="small" (click)="allocState.edit(a)">Edit</button> <button nz-button nzSize="small" nzDanger (click)="removeAlloc(a)">Delete</button></td></tr>
            } @empty { <tr><td colspan="6">Nobody is allocated yet.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
    </nz-tabs>
    <erp-record-drawer [title]="rateState.editing() ? 'Edit rate' : 'New rate'" [open]="rateState.open()" [fields]="rateFields()" [value]="$any(rateState.editing())" [saving]="rateState.saving()" [errors]="rateState.errors()" testId="rate-form" (submitted)="rateState.submit($event)" (closed)="rateState.close()" />
    <erp-record-drawer [title]="allocState.editing() ? 'Edit allocation' : 'New allocation'" [open]="allocState.open()" [fields]="allocFields()" [value]="$any(allocState.editing())" [saving]="allocState.saving()" [errors]="allocState.errors()" testId="alloc-form" (submitted)="allocState.submit($event)" (closed)="allocState.close()" />
  `,
  styles: ['.hint { color:#64748b; font-size:12.5px; margin-bottom:12px }'],
})
export class ProjectResourcesPage {
  private readonly api = inject(ProjectsExtraApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly rates = signal<RateDto[]>([]);
  readonly allocations = signal<AllocationDto[]>([]);
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.api.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly rateState = new MasterDataState<RateDto>({ save: (id, body) => this.api.saveRate(id, body as never), reload: () => this.load(), message: this.msg, entityName: 'Rate' });
  readonly allocState = new MasterDataState<AllocationDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) };
      for (const k of ['fromDate', 'toDate']) if (b[k] instanceof Date) b[k] = toIsoDate(b[k] as Date);
      return this.api.saveAllocation(id, b as never);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Allocation',
  });

  readonly rateFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project (blank = every project)', type: 'select', options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'userId', label: 'Person', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'role', label: 'or a role', type: 'text', maxLength: 100, hint: 'For example Senior consultant. A rate is for a person or a role, not both.', hidden: (v) => !!v['userId'] },
    { key: 'billRate', label: 'Bill rate per hour', type: 'number', required: true, min: 0, precision: 2 },
    { key: 'costRate', label: 'Cost rate per hour', type: 'number', required: true, min: 0, precision: 2 },
  ]);
  readonly allocFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'userId', label: 'Person', type: 'select', required: true, options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'fromDate', label: 'From', type: 'date', required: true, default: new Date() },
    { key: 'toDate', label: 'To', type: 'date', required: true, default: new Date(Date.now() + 30 * 864e5) },
    { key: 'hoursPerWeek', label: 'Hours per week', type: 'number', required: true, min: 1, max: 168, default: 20 },
  ]);

  constructor() { this.load(); }

  load(): void {
    this.api.rates().subscribe((r) => this.rates.set(r));
    this.api.allocations().subscribe((r) => this.allocations.set(r));
  }
  projectName(id?: string | null): string { if (!id) return 'All projects'; const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  removeRate(r: RateDto): void { this.api.deleteRate(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete.') }); }
  removeAlloc(a: AllocationDto): void { this.api.deleteAllocation(a.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete.') }); }
}
